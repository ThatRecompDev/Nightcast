// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// Whether a source of the console's pad is being used: a button pressed, or a stick or trigger past
/// <c>threshold</c>. Keyboard and mouse are used at any value (their pad rests at exactly zero); a controller's
/// threshold sits well above stick drift and a trigger's resting pull, so a pad lying on the desk never takes over.
/// </summary>
public static class PadActivity
{
    /// <summary>How far a controller's stick or trigger moves before it counts as used: far past drift.</summary>
    public const float ControllerThreshold = 0.3f;

    [HotPath]
    public static bool IsUsed(in XboxPadState pad, float threshold)
    {
        if (pad.Buttons != XboxButtons.None)
        {
            return true;
        }

        float squared = threshold * threshold;

        if ((pad.LeftX * pad.LeftX) + (pad.LeftY * pad.LeftY) > squared)
        {
            return true;
        }

        if ((pad.RightX * pad.RightX) + (pad.RightY * pad.RightY) > squared)
        {
            return true;
        }

        return pad.LeftTrigger > threshold || pad.RightTrigger > threshold;
    }
}
