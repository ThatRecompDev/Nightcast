// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// The console's vibration on the player's hands: each report for the console's pad 0 runs the motors of the physical
/// pad being played (<see cref="IPadComposer.PlayedSlot"/>) at the report's strengths, and they stop when its duration
/// ends, when a report says 0 and 0, or when the player picks up another pad. A report without a duration runs until
/// the next one, at most <see cref="LongestUntimed"/>, so a lost stop never leaves the pad shaking. Render thread only;
/// nothing allocates.
/// </summary>
public sealed class RumblePlayer
{
    /// <summary>The longest a report without a duration runs before the motors stop on their own.</summary>
    public static readonly TimeSpan LongestUntimed = TimeSpan.FromSeconds(2);

    private const int NoSlot = -1;
    private const byte PlayedPad = 0;

    private readonly IGamepadFeedback? _feedback;
    private int _slot = NoSlot;
    private TimeSpan _stopAt;

    /// <param name="feedback">The pads' motors; null where the pads have none (the motors are then left alone).</param>
    public RumblePlayer(IGamepadFeedback? feedback)
    {
        _feedback = feedback;
    }

    /// <summary>Whether motors run now.</summary>
    public bool Running => _slot != NoSlot;

    /// <summary>A report from the console, at <paramref name="now"/>, with <paramref name="playedSlot"/> the pad played.</summary>
    [HotPath]
    public void Play(in RumbleReport report, int playedSlot, TimeSpan now)
    {
        if (_feedback is null || report.PadIndex != PlayedPad)
        {
            return;
        }

        if (playedSlot == NoSlot || (report.LowFrequency <= 0f && report.HighFrequency <= 0f))
        {
            Stop();

            return;
        }

        if (_slot != NoSlot && _slot != playedSlot)
        {
            Stop();
        }

        _feedback.TrySetRumble(playedSlot, report.LowFrequency, report.HighFrequency);
        _slot = playedSlot;
        _stopAt = now + Length(report.Duration);
    }

    /// <summary>Once a frame: the motors stop when the report's time is up or the player took up another pad.</summary>
    [HotPath]
    public void Tick(int playedSlot, TimeSpan now)
    {
        if (_slot != NoSlot && (now >= _stopAt || playedSlot != _slot))
        {
            Stop();
        }
    }

    /// <summary>The motors stop now (the stream ended, the window lost the pad).</summary>
    public void Stop()
    {
        if (_feedback is null || _slot == NoSlot)
        {
            return;
        }

        _feedback.TrySetRumble(_slot, 0f, 0f);
        _slot = NoSlot;
    }

    private static TimeSpan Length(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return LongestUntimed;
        }

        return duration;
    }
}
