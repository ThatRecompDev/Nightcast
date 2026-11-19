// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// Movement: four keys push the left stick fully (a diagonal is normalised to full tilt, not 1.41), the walk key
/// scales that tilt down to <see cref="WalkTilt"/> (0..1), and the sprint key drives <see cref="SprintControl"/>.
/// </summary>
public sealed record MovementSettings(
    Key Forward,
    Key Back,
    Key Left,
    Key Right,
    Key Walk,
    float WalkTilt,
    Key Sprint,
    PadControl SprintControl)
{
    /// <summary>Half tilt: a walk in most games, whose run starts past about two thirds of the stick's travel.</summary>
    public const float StandardWalkTilt = 0.5f;

    /// <summary>WASD, left Alt to walk, left Shift to sprint on A.</summary>
    public static MovementSettings Wasd { get; } = new(
        Key.W,
        Key.S,
        Key.A,
        Key.D,
        Key.LeftAlt,
        StandardWalkTilt,
        Key.LeftShift,
        PadControl.A);
}
