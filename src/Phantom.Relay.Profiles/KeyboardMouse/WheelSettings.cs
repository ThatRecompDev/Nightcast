// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// The wheel: each notch presses its binding's control for <see cref="PressSeconds"/>, then lets go for as long, so
/// quick notches arrive as separate presses.
/// </summary>
public sealed record WheelSettings(float PressSeconds, IReadOnlyList<WheelBinding> Bindings)
{
    /// <summary>Three frames at 60 Hz: long enough for a game that reads its pad once a frame.</summary>
    public const float StandardPressSeconds = 0.05f;

    public static WheelSettings None { get; } = new(StandardPressSeconds, []);
}
