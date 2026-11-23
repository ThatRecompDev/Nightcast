// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Stats;

/// <summary>How much of the stats shows over the picture; F3 and the menu step through them in this order.</summary>
public enum StatsLevel
{
    Off,

    /// <summary>One line: the frame rate and the latency.</summary>
    Small,

    /// <summary>A few lines: the frame rate, the latency's parts, the bitrate and losses, what improves the picture.</summary>
    Large,

    /// <summary>Everything measured, for finding a problem.</summary>
    Detailed,
}
