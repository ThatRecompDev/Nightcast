// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// A <see cref="ResponseCurve"/> as arithmetic: a stick's travel past the dead zone (0..1) to the travel sent (0..1).
/// Every curve keeps 0 at 0 and 1 at 1 and never falls as the travel grows.
/// </summary>
public static class ResponseCurves
{
    /// <summary>Smoothstep is t² (3 - 2t): this is its 3.</summary>
    private const float SmoothstepRise = 3f;

    /// <summary>Smoothstep's 2.</summary>
    private const float SmoothstepBend = 2f;

    [HotPath]
    public static float Apply(ResponseCurve curve, float travel)
    {
        var t = Math.Clamp(travel, 0f, 1f);

        switch (curve)
        {
            case ResponseCurve.Smooth:
                return t * t * (SmoothstepRise - (SmoothstepBend * t));
            case ResponseCurve.Precise:
                return t * t;
            case ResponseCurve.Aggressive:
                return MathF.Sqrt(t);
            default:
                return t;
        }
    }
}
