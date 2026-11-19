// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The d-pad as a PlayStation pad reports it: a HID hat switch in the low four bits of the first button byte, 0 for up
/// and then clockwise in eighths (1 up-right, 2 right, ... 7 up-left), 8 when nothing is held.
/// </summary>
public static class PadHat
{
    /// <summary>The hat's bits in its byte.</summary>
    public const int Mask = 0x0F;

    private const int Up = 0;
    private const int UpRight = 1;
    private const int Right = 2;
    private const int DownRight = 3;
    private const int Down = 4;
    private const int DownLeft = 5;
    private const int Left = 6;
    private const int UpLeft = 7;

    /// <summary>The d-pad buttons a hat value holds; none for the released value and any value past it.</summary>
    [HotPath]
    public static GamepadButtons Buttons(int hat)
    {
        switch (hat)
        {
            case Up:
                return GamepadButtons.DpadUp;

            case UpRight:
                return GamepadButtons.DpadUp | GamepadButtons.DpadRight;

            case Right:
                return GamepadButtons.DpadRight;

            case DownRight:
                return GamepadButtons.DpadDown | GamepadButtons.DpadRight;

            case Down:
                return GamepadButtons.DpadDown;

            case DownLeft:
                return GamepadButtons.DpadDown | GamepadButtons.DpadLeft;

            case Left:
                return GamepadButtons.DpadLeft;

            case UpLeft:
                return GamepadButtons.DpadUp | GamepadButtons.DpadLeft;

            default:
                return GamepadButtons.None;
        }
    }
}
