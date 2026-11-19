// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// What a key, a mouse button or a wheel notch drives on the emulated pad: a button, a trigger (pulled fully) or one
/// direction of a stick (pushed fully). The stick directions are contiguous, left stick first, so a direction's index
/// is its distance from <see cref="LeftStickUp"/>.
/// </summary>
public enum PadControl
{
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    LeftThumb,
    RightThumb,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Menu,
    View,
    Nexus,
    LeftTrigger,
    RightTrigger,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
}
