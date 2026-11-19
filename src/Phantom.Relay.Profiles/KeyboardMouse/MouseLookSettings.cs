// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// How the mouse drives the right stick. A stick is a position the game turns into turning speed, so the mouse's
/// speed (counts per second) becomes the tilt:
/// <list type="number">
/// <item>speed times <see cref="SensitivityX"/> or <see cref="SensitivityY"/>, divided by <see cref="FullTiltSpeed"/>
/// (the speed that tilts the stick fully at sensitivity 1), times <see cref="AimMultiplier"/> while
/// <see cref="AimButton"/> is held;</item>
/// <item>that length, clamped to 1, raised to <see cref="Exponent"/> (below 1 lifts slow moves, above 1 lowers
/// them);</item>
/// <item>lifted past the game's own radial dead zone: tilt = <see cref="AntiDeadZone"/> + (1 - it) times the curve,
/// so the smallest move already turns the camera;</item>
/// <item>smoothed over <see cref="SmoothingFrames"/> frames (0 to 2; 0 is raw), and when the mouse stops, brought back
/// to the centre in at most <see cref="DecaySeconds"/> from full tilt.</item>
/// </list>
/// The mouse's y grows downwards; the stick's grows upwards, so a move away from the player looks up unless
/// <see cref="InvertY"/>.
/// </summary>
public sealed record MouseLookSettings(
    float SensitivityX,
    float SensitivityY,
    float FullTiltSpeed,
    float Exponent,
    float AntiDeadZone,
    float SmoothingFrames,
    float DecaySeconds,
    float AimMultiplier,
    PointerButton AimButton,
    bool InvertY)
{
    /// <summary>The most smoothing: more than two frames of lag is felt in a shooter.</summary>
    public const float MaxSmoothingFrames = 2f;

    /// <summary>Counts per second for full tilt: an 800 DPI mouse moved 2.5 inches a second.</summary>
    public const float StandardFullTiltSpeed = 2000f;

    /// <summary>Lifts slow moves, partly undoing the near-square response curve most console games give their sticks.</summary>
    public const float StandardExponent = 0.6f;

    /// <summary>A quarter of the travel: the middle of the 0.2 to 0.3 radial dead zones console games use.</summary>
    public const float StandardAntiDeadZone = 0.25f;

    /// <summary>One frame of smoothing: evens out a mouse polled slower than the frame rate.</summary>
    public const float StandardSmoothingFrames = 1f;

    /// <summary>Three frames at 60 Hz from full tilt to the centre: the camera stops when the hand does.</summary>
    public const float StandardDecaySeconds = 0.05f;

    /// <summary>Slower turning while aiming, for fine corrections on the target.</summary>
    public const float StandardAimMultiplier = 0.6f;

    public static MouseLookSettings Standard { get; } = new(
        1f,
        1f,
        StandardFullTiltSpeed,
        StandardExponent,
        StandardAntiDeadZone,
        StandardSmoothingFrames,
        StandardDecaySeconds,
        StandardAimMultiplier,
        PointerButton.Right,
        InvertY: false);
}
