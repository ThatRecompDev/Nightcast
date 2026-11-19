// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// Keyboard and mouse played as the console's pad, per profile: movement keys on the left stick, the mouse on the
/// right stick, and keys, mouse buttons and wheel notches bound to buttons, triggers and stick directions. Several
/// sources may drive one control; a stick's directions add up and are clamped to full tilt.
/// </summary>
public sealed record KeyboardMouseSettings(
    bool Enabled,
    MovementSettings Movement,
    MouseLookSettings MouseLook,
    IReadOnlyList<KeyBinding> Keys,
    IReadOnlyList<MouseButtonBinding> MouseButtons,
    WheelSettings Wheel)
{
    /// <summary>Switched off: the pad alone plays. Movement and mouse look keep their standard values for when it is switched on.</summary>
    public static KeyboardMouseSettings Off { get; } = new(
        Enabled: false,
        MovementSettings.Wasd,
        MouseLookSettings.Standard,
        [],
        [],
        WheelSettings.None);
}
