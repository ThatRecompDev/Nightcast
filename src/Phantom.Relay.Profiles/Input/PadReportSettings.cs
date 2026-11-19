// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// When pad reports go out (<see cref="PadReportPolicy"/>): an unchanged pad is sent again every
/// <see cref="KeepAlive"/>, so a lost report is healed; a stick or trigger that moved less than
/// <see cref="StickEpsilon"/> (in -1..1 units) has not changed; and with <see cref="NeutralOnFocusLoss"/> a window
/// without focus sends the pad at rest, so nothing stays held while the player is elsewhere.
/// </summary>
public sealed record PadReportSettings(TimeSpan KeepAlive, float StickEpsilon, bool NeutralOnFocusLoss)
{
    /// <summary>Ten reports a second while nothing changes: a lost report heals within a tenth of a second.</summary>
    public const int StandardKeepAliveMilliseconds = 100;

    /// <summary>One step of the report's 16-bit axis: a smaller move cannot change what is sent.</summary>
    public const float StandardStickEpsilon = 1f / short.MaxValue;

    public static PadReportSettings Standard { get; } = new(
        TimeSpan.FromMilliseconds(StandardKeepAliveMilliseconds),
        StandardStickEpsilon,
        NeutralOnFocusLoss: true);
}
