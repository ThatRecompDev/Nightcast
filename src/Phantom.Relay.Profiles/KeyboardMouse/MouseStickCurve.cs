// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// The tilt a mouse speed gives the stick (<see cref="MouseLookSettings"/>): 0 at rest, else the speed clamped to 1,
/// raised to the exponent and lifted past the game's dead zone. It never decreases with speed and never passes 1.
/// </summary>
public static class MouseStickCurve
{
    /// <param name="speed">The mouse's speed after sensitivity, 1 being full tilt.</param>
    /// <param name="exponent">The curve's exponent, above 0.</param>
    /// <param name="antiDeadZone">The game's radial dead zone to start past, 0 to below 1.</param>
    [HotPath]
    public static float Tilt(float speed, float exponent, float antiDeadZone)
    {
        if (!(speed > 0f))
        {
            return 0f;
        }

        float curved = MathF.Pow(MathF.Min(speed, 1f), exponent);

        return antiDeadZone + ((1f - antiDeadZone) * curved);
    }
}
