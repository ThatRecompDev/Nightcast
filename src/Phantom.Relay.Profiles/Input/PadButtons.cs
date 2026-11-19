// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// The engine's buttons, named by their place on the pad, as the console's: South is A, East B, West X, North Y; Back
/// is View, Start is Menu, Guide is Nexus, and a touchpad's click is View too (the DualSense's share button is Back).
/// The digital trigger bits and a pad's extra button have no console button: the triggers travel as values.
/// </summary>
public static class PadButtons
{
    /// <summary>Every held place as its console button.</summary>
    [HotPath]
    public static XboxButtons ByPlace(GamepadButtons held)
    {
        var buttons = XboxButtons.None;

        for (var place = GamepadButtons.South; place <= GamepadButtons.Misc; place = (GamepadButtons)((int)place << 1))
        {
            if ((held & place) != 0)
            {
                buttons |= Of(place);
            }
        }

        return buttons;
    }

    /// <summary>The console's button at <paramref name="place"/> (one place); none where the console has none.</summary>
    [HotPath]
    public static XboxButtons Of(GamepadButtons place)
    {
        switch (place)
        {
            case GamepadButtons.South:
                return XboxButtons.A;
            case GamepadButtons.East:
                return XboxButtons.B;
            case GamepadButtons.West:
                return XboxButtons.X;
            case GamepadButtons.North:
                return XboxButtons.Y;
            case GamepadButtons.LeftShoulder:
                return XboxButtons.LeftShoulder;
            case GamepadButtons.RightShoulder:
                return XboxButtons.RightShoulder;
            case GamepadButtons.Back:
                return XboxButtons.View;
            case GamepadButtons.Start:
                return XboxButtons.Menu;
            case GamepadButtons.LeftStick:
                return XboxButtons.LeftThumb;
            case GamepadButtons.RightStick:
                return XboxButtons.RightThumb;
            case GamepadButtons.DpadUp:
                return XboxButtons.DPadUp;
            case GamepadButtons.DpadDown:
                return XboxButtons.DPadDown;
            case GamepadButtons.DpadLeft:
                return XboxButtons.DPadLeft;
            case GamepadButtons.DpadRight:
                return XboxButtons.DPadRight;
            case GamepadButtons.Guide:
                return XboxButtons.Nexus;
            case GamepadButtons.Touchpad:
                return XboxButtons.View;
            default:
                return XboxButtons.None;
        }
    }
}
