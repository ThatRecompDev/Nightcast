// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Linux.Events;

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// The kernel's gamepad key codes as the engine's buttons by place. South (<c>BTN_SOUTH</c>) is the bottom face button
/// on every driver and East the right one; 0x133 and 0x134 depend on the driver's <see cref="FaceLayout"/>.
/// <c>BTN_TL2</c>/<c>BTN_TR2</c> are the triggers' digital press, <c>BTN_MODE</c> the Xbox or PS button.
/// </summary>
internal static class EvdevButtons
{
    /// <summary>Every key code read, for a resynchronisation after a drop.</summary>
    public static readonly ushort[] Codes =
    [
        EvdevCodes.ButtonSouth, EvdevCodes.ButtonEast, EvdevCodes.ButtonX, EvdevCodes.ButtonY,
        EvdevCodes.ButtonLeftShoulder, EvdevCodes.ButtonRightShoulder, EvdevCodes.ButtonLeftTrigger, EvdevCodes.ButtonRightTrigger,
        EvdevCodes.ButtonSelect, EvdevCodes.ButtonStart, EvdevCodes.ButtonMode, EvdevCodes.ButtonLeftThumb,
        EvdevCodes.ButtonRightThumb, EvdevCodes.ButtonDpadUp, EvdevCodes.ButtonDpadDown, EvdevCodes.ButtonDpadLeft,
        EvdevCodes.ButtonDpadRight,
    ];

    /// <summary>The engine button a key code is; none for a code a pad's state does not carry.</summary>
    [HotPath]
    public static GamepadButtons Map(ushort code, FaceLayout layout)
    {
        switch (code)
        {
            case EvdevCodes.ButtonSouth:
                return GamepadButtons.South;

            case EvdevCodes.ButtonEast:
                return GamepadButtons.East;

            case EvdevCodes.ButtonX:
                return FaceButton(layout, GamepadButtons.West, GamepadButtons.North);

            case EvdevCodes.ButtonY:
                return FaceButton(layout, GamepadButtons.North, GamepadButtons.West);

            default:
                return Other(code);
        }
    }

    [HotPath]
    private static GamepadButtons FaceButton(FaceLayout layout, GamepadButtons labelled, GamepadButtons positional)
    {
        if (layout == FaceLayout.XboxLabels)
        {
            return labelled;
        }

        return positional;
    }

    [HotPath]
    private static GamepadButtons Other(ushort code)
    {
        switch (code)
        {
            case EvdevCodes.ButtonLeftShoulder:
                return GamepadButtons.LeftShoulder;

            case EvdevCodes.ButtonRightShoulder:
                return GamepadButtons.RightShoulder;

            case EvdevCodes.ButtonLeftTrigger:
                return GamepadButtons.LeftTrigger;

            case EvdevCodes.ButtonRightTrigger:
                return GamepadButtons.RightTrigger;

            case EvdevCodes.ButtonSelect:
                return GamepadButtons.Back;

            case EvdevCodes.ButtonStart:
                return GamepadButtons.Start;

            case EvdevCodes.ButtonMode:
                return GamepadButtons.Guide;

            default:
                return Sticks(code);
        }
    }

    [HotPath]
    private static GamepadButtons Sticks(ushort code)
    {
        switch (code)
        {
            case EvdevCodes.ButtonLeftThumb:
                return GamepadButtons.LeftStick;

            case EvdevCodes.ButtonRightThumb:
                return GamepadButtons.RightStick;

            case EvdevCodes.ButtonDpadUp:
                return GamepadButtons.DpadUp;

            case EvdevCodes.ButtonDpadDown:
                return GamepadButtons.DpadDown;

            case EvdevCodes.ButtonDpadLeft:
                return GamepadButtons.DpadLeft;

            case EvdevCodes.ButtonDpadRight:
                return GamepadButtons.DpadRight;

            default:
                return GamepadButtons.None;
        }
    }
}
