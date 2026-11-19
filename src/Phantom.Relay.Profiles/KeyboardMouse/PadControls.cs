// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>What a <see cref="PadControl"/> is on the console's pad: a button bit, a trigger or a stick direction.</summary>
public static class PadControls
{
    /// <summary>How many stick directions there are: four per stick.</summary>
    public const int StickDirections = PadControl.RightStickRight - PadControl.LeftStickUp + 1;

    public static bool IsStickDirection(PadControl control)
    {
        return control >= PadControl.LeftStickUp && control <= PadControl.RightStickRight;
    }

    /// <summary>The direction's index, 0 to <see cref="StickDirections"/> minus 1, in <see cref="PadControl"/> order.</summary>
    public static int DirectionIndex(PadControl control)
    {
        return control - PadControl.LeftStickUp;
    }

    /// <summary>The button bit of <paramref name="control"/>; none for a trigger or a stick direction.</summary>
    [HotPath]
    public static XboxButtons ButtonOf(PadControl control)
    {
        switch (control)
        {
            case PadControl.A:
                return XboxButtons.A;
            case PadControl.B:
                return XboxButtons.B;
            case PadControl.X:
                return XboxButtons.X;
            case PadControl.Y:
                return XboxButtons.Y;
            case PadControl.LeftShoulder:
                return XboxButtons.LeftShoulder;
            case PadControl.RightShoulder:
                return XboxButtons.RightShoulder;
            case PadControl.LeftThumb:
                return XboxButtons.LeftThumb;
            case PadControl.RightThumb:
                return XboxButtons.RightThumb;
            case PadControl.DPadUp:
                return XboxButtons.DPadUp;
            case PadControl.DPadDown:
                return XboxButtons.DPadDown;
            case PadControl.DPadLeft:
                return XboxButtons.DPadLeft;
            case PadControl.DPadRight:
                return XboxButtons.DPadRight;
            case PadControl.Menu:
                return XboxButtons.Menu;
            case PadControl.View:
                return XboxButtons.View;
            case PadControl.Nexus:
                return XboxButtons.Nexus;
            default:
                return XboxButtons.None;
        }
    }
}
