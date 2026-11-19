// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// The engine's first pad slot as the console's pad, as it is: each button by its place, the sticks with y turned up
/// (the engine's pads are y down, the console's y up), the triggers' travel. No dead zone and no curve: the console's
/// game applies its own. A pad that is not connected reads as a pad at rest.
/// </summary>
public sealed class DirectPadComposer : IPadComposer
{
    private static readonly (GamepadButtons Engine, XboxButtons Console)[] Buttons =
    [
        (GamepadButtons.South, XboxButtons.A),
        (GamepadButtons.East, XboxButtons.B),
        (GamepadButtons.West, XboxButtons.X),
        (GamepadButtons.North, XboxButtons.Y),
        (GamepadButtons.LeftShoulder, XboxButtons.LeftShoulder),
        (GamepadButtons.RightShoulder, XboxButtons.RightShoulder),
        (GamepadButtons.Back, XboxButtons.View),
        (GamepadButtons.Start, XboxButtons.Menu),
        (GamepadButtons.Guide, XboxButtons.Nexus),
        (GamepadButtons.LeftStick, XboxButtons.LeftThumb),
        (GamepadButtons.RightStick, XboxButtons.RightThumb),
        (GamepadButtons.DpadUp, XboxButtons.DPadUp),
        (GamepadButtons.DpadDown, XboxButtons.DPadDown),
        (GamepadButtons.DpadLeft, XboxButtons.DPadLeft),
        (GamepadButtons.DpadRight, XboxButtons.DPadRight),
    ];

    [HotPath]
    public XboxPadState Compose(InputFrame input)
    {
        var pads = input.Gamepads;

        if (pads.IsEmpty || !pads[0].Connected)
        {
            return default;
        }

        var pad = pads[0];

        return new XboxPadState(
            ButtonsOf(pad.Buttons),
            pad.LeftStick.X,
            -pad.LeftStick.Y,
            pad.RightStick.X,
            -pad.RightStick.Y,
            pad.LeftTrigger,
            pad.RightTrigger);
    }

    [HotPath]
    private static XboxButtons ButtonsOf(GamepadButtons held)
    {
        var buttons = XboxButtons.None;

        for (int i = 0; i < Buttons.Length; i++)
        {
            if ((held & Buttons[i].Engine) != 0)
            {
                buttons |= Buttons[i].Console;
            }
        }

        return buttons;
    }
}
