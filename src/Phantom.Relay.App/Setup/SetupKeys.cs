// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Setup;

/// <summary>What the player pressed on the setup screen this frame, from the keys or any pad.</summary>
[Flags]
public enum SetupKeys
{
    None = 0,

    /// <summary>Up: the arrow key or the d-pad.</summary>
    Up = 0x1,

    /// <summary>Down: the arrow key or the d-pad.</summary>
    Down = 0x2,

    /// <summary>Choose: Enter or A (the bottom face button).</summary>
    Choose = 0x4,

    /// <summary>Back: Escape or B (the right face button).</summary>
    Back = 0x8,
}
