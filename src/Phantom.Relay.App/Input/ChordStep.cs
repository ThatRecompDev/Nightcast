// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Input;

/// <summary>What Relay's pad chord does in a frame (<see cref="MenuChord"/>).</summary>
public enum ChordStep
{
    /// <summary>No chord: the pad is the game's.</summary>
    None,

    /// <summary>The chord is held: the console gets the pad at rest.</summary>
    Held,

    /// <summary>Let go in time: Relay's menu opens.</summary>
    OpenMenu,

    /// <summary>Held long enough: the stream ends.</summary>
    Quit,
}
