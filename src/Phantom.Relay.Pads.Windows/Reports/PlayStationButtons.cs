// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The three button bytes the DualSense and the DualShock 4 share, as the engine's buttons by place:
/// <code>
/// face:    bits 0-3 hat | 4 square | 5 cross | 6 circle | 7 triangle
/// shoulder: 0 L1 | 1 R1 | 2 L2 | 3 R2 | 4 create (share) | 5 options | 6 L3 | 7 R3
/// system:  0 PS | 1 touchpad click | 2 mute (DualSense; a DualShock 4 counts reports in bits 2-7)
/// </code>
/// Cross is the bottom face button (South), circle the right (East), square the left (West), triangle the top.
/// </summary>
public static class PlayStationButtons
{
    private const int Square = 0x10;
    private const int Cross = 0x20;
    private const int Circle = 0x40;
    private const int Triangle = 0x80;

    private const int L1 = 0x01;
    private const int R1 = 0x02;
    private const int L2 = 0x04;
    private const int R2 = 0x08;
    private const int Create = 0x10;
    private const int Options = 0x20;
    private const int L3 = 0x40;
    private const int R3 = 0x80;

    private const int PlayStation = 0x01;
    private const int TouchpadClick = 0x02;
    private const int Mute = 0x04;

    /// <summary>The buttons held; <paramref name="hasMute"/> reads the DualSense's microphone button as Misc.</summary>
    [HotPath]
    public static GamepadButtons Map(byte face, byte shoulder, byte system, bool hasMute)
    {
        var held = PadHat.Buttons(face & PadHat.Mask);
        held |= Bit(face, Cross, GamepadButtons.South) | Bit(face, Circle, GamepadButtons.East);
        held |= Bit(face, Square, GamepadButtons.West) | Bit(face, Triangle, GamepadButtons.North);
        held |= Bit(shoulder, L1, GamepadButtons.LeftShoulder) | Bit(shoulder, R1, GamepadButtons.RightShoulder);
        held |= Bit(shoulder, L2, GamepadButtons.LeftTrigger) | Bit(shoulder, R2, GamepadButtons.RightTrigger);
        held |= Bit(shoulder, Create, GamepadButtons.Back) | Bit(shoulder, Options, GamepadButtons.Start);

        held |= Bit(shoulder, L3, GamepadButtons.LeftStick) | Bit(shoulder, R3, GamepadButtons.RightStick);
        held |= Bit(system, PlayStation, GamepadButtons.Guide) | Bit(system, TouchpadClick, GamepadButtons.Touchpad);

        if (hasMute)
        {
            held |= Bit(system, Mute, GamepadButtons.Misc);
        }

        return held;
    }

    [HotPath]
    private static GamepadButtons Bit(byte value, int bit, GamepadButtons button)
    {
        if ((value & bit) != 0)
        {
            return button;
        }

        return GamepadButtons.None;
    }
}
