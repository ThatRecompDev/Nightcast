// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// Two sources of the console's pad as one, so a player can mix them: the buttons of both; per stick, the source
/// pushed further (both axes from it, so a stick never points between two hands); per trigger, the one pulled
/// further. A tie goes to the physical pad.
/// </summary>
public static class PadMerge
{
    [HotPath]
    public static XboxPadState Merge(in XboxPadState pad, in XboxPadState keyboardMouse)
    {
        var padLeft = Deflection(pad.LeftX, pad.LeftY) >= Deflection(keyboardMouse.LeftX, keyboardMouse.LeftY);
        var padRight = Deflection(pad.RightX, pad.RightY) >= Deflection(keyboardMouse.RightX, keyboardMouse.RightY);
        var left = padLeft ? pad : keyboardMouse;
        var right = padRight ? pad : keyboardMouse;

        return new XboxPadState(
            pad.Buttons | keyboardMouse.Buttons,
            left.LeftX,
            left.LeftY,
            right.RightX,
            right.RightY,
            MathF.Max(pad.LeftTrigger, keyboardMouse.LeftTrigger),
            MathF.Max(pad.RightTrigger, keyboardMouse.RightTrigger));
    }

    /// <summary>The stick's squared length: it orders deflections as the length does, without a root.</summary>
    [HotPath]
    private static float Deflection(float x, float y)
    {
        return (x * x) + (y * y);
    }
}
