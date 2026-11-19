// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// Decides when a pad report goes out (<see cref="PadReportSettings"/>), with the time passed in, so it reads no
/// clock:
/// <list type="bullet">
/// <item>a change goes out at once, the moment it is composed: nothing waits for the frame's end or is batched;</item>
/// <item>a stick or trigger that moved less than the epsilon since the last report sent has not changed (moves add
/// up against that report, so a slow drift still goes out); a button change always counts;</item>
/// <item>an unchanged pad goes out again once the keep-alive has passed since the last report, so a lost report is
/// healed;</item>
/// <item>with the neutral-on-focus-loss rule, a window without focus counts as the pad at rest: that goes out once,
/// as a change, when focus is lost, and then only as the keep-alive, until focus returns.</item>
/// </list>
/// The first pad always goes out. One policy per pad and channel; <see cref="Reset"/> when the channel opens again.
/// </summary>
public sealed class PadReportPolicy
{
    private readonly TimeSpan _keepAlive;
    private readonly float _epsilon;
    private readonly bool _neutralOnFocusLoss;
    private XboxPadState _sent;
    private TimeSpan _sentAt;
    private bool _hasSent;

    public PadReportPolicy(PadReportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(settings.KeepAlive, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.StickEpsilon);

        _keepAlive = settings.KeepAlive;
        _epsilon = settings.StickEpsilon;
        _neutralOnFocusLoss = settings.NeutralOnFocusLoss;
    }

    /// <summary>
    /// Whether to send a report for <paramref name="state"/>, composed at <paramref name="now"/> (any monotonic
    /// time), with the window <paramref name="focused"/> or not; <paramref name="report"/> is the pad to send. When it
    /// returns true the report counts as sent.
    /// </summary>
    [HotPath]
    public bool ShouldSend(in XboxPadState state, bool focused, TimeSpan now, out XboxPadState report)
    {
        report = state;

        if (!focused && _neutralOnFocusLoss)
        {
            report = default;
        }

        if (_hasSent && !Differs(in report, in _sent) && now - _sentAt < _keepAlive)
        {
            return false;
        }

        _sent = report;
        _sentAt = now;
        _hasSent = true;

        return true;
    }

    /// <summary>Forgets the last report: the next pad goes out at once (a new channel knows nothing yet).</summary>
    public void Reset()
    {
        _hasSent = false;
    }

    [HotPath]
    private bool Differs(in XboxPadState a, in XboxPadState b)
    {
        if (a.Buttons != b.Buttons)
        {
            return true;
        }

        return Moved(a.LeftX, b.LeftX)
            || Moved(a.LeftY, b.LeftY)
            || Moved(a.RightX, b.RightX)
            || Moved(a.RightY, b.RightY)
            || Moved(a.LeftTrigger, b.LeftTrigger)
            || Moved(a.RightTrigger, b.RightTrigger);
    }

    [HotPath]
    private bool Moved(float a, float b)
    {
        var difference = MathF.Abs(a - b);

        return difference > 0f && difference >= _epsilon;
    }
}
