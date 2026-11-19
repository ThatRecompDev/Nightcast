// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles;

/// <summary>
/// The pad: stick and trigger dead zones (0..1), the stick curve, and buttons sent as others (unlisted buttons pass
/// through). The dead zone is applied with the engine's radial stick shaping.
/// </summary>
public sealed record PadSettings(
    float StickDeadZone,
    float TriggerDeadZone,
    ResponseCurve Curve,
    IReadOnlyDictionary<XboxButtons, XboxButtons> Remap)
{
    public const float StandardStickDeadZone = 0.08f;

    public const float StandardTriggerDeadZone = 0.02f;

    public static PadSettings Standard { get; } = new(
        StandardStickDeadZone,
        StandardTriggerDeadZone,
        ResponseCurve.Linear,
        new Dictionary<XboxButtons, XboxButtons>());
}
