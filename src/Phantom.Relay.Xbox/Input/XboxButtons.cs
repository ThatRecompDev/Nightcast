// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Input;

/// <summary>The button bits of a gamepad report on the input channel.</summary>
[Flags]
public enum XboxButtons : ushort
{
    None = 0,
    Nexus = 1 << 1,
    Menu = 1 << 2,
    View = 1 << 3,
    A = 1 << 4,
    B = 1 << 5,
    X = 1 << 6,
    Y = 1 << 7,
    DPadUp = 1 << 8,
    DPadDown = 1 << 9,
    DPadLeft = 1 << 10,
    DPadRight = 1 << 11,
    LeftShoulder = 1 << 12,
    RightShoulder = 1 << 13,
    LeftThumb = 1 << 14,
    RightThumb = 1 << 15,
}
