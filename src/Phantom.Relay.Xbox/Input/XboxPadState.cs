// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Input;

/// <summary>
/// One pad as the console reads it: sticks -1..1 with +Y up (the console's convention, the engine's pads are y down),
/// triggers 0..1.
/// </summary>
public readonly record struct XboxPadState(
    XboxButtons Buttons,
    float LeftX,
    float LeftY,
    float RightX,
    float RightY,
    float LeftTrigger,
    float RightTrigger);
