// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests.Mapping;

/// <summary>Pads as their kernel drivers describe them, written by hand from the drivers' published ranges.</summary>
internal static class Descriptions
{
    /// <summary>Steam Input's virtual Xbox 360 pad (xpad's ranges): sticks ±32767, triggers 0-255, the hat ±1, rumble.</summary>
    public static EvdevDescription SteamVirtualPad()
    {
        var axes = new EvdevAxisRange[EvdevCodes.AxisCount];
        axes[EvdevCodes.AxisX] = new EvdevAxisRange(0, -32768, 32767);
        axes[EvdevCodes.AxisY] = new EvdevAxisRange(0, -32768, 32767);
        axes[EvdevCodes.AxisRx] = new EvdevAxisRange(0, -32768, 32767);
        axes[EvdevCodes.AxisRy] = new EvdevAxisRange(0, -32768, 32767);

        axes[EvdevCodes.AxisZ] = new EvdevAxisRange(0, 0, 255);
        axes[EvdevCodes.AxisRz] = new EvdevAxisRange(0, 0, 255);
        axes[EvdevCodes.AxisHatX] = new EvdevAxisRange(0, -1, 1);
        axes[EvdevCodes.AxisHatY] = new EvdevAxisRange(0, -1, 1);

        return new EvdevDescription(EvdevDescription.Valve, 0x11FF, axes, Rumble: true);
    }

    /// <summary>A DualSense through hid-playstation: sticks and triggers 0-255 (sticks resting at 128), the hat ±1.</summary>
    public static EvdevDescription DualSense()
    {
        var axes = new EvdevAxisRange[EvdevCodes.AxisCount];
        axes[EvdevCodes.AxisX] = new EvdevAxisRange(128, 0, 255);
        axes[EvdevCodes.AxisY] = new EvdevAxisRange(128, 0, 255);
        axes[EvdevCodes.AxisRx] = new EvdevAxisRange(128, 0, 255);
        axes[EvdevCodes.AxisRy] = new EvdevAxisRange(128, 0, 255);

        axes[EvdevCodes.AxisZ] = new EvdevAxisRange(0, 0, 255);
        axes[EvdevCodes.AxisRz] = new EvdevAxisRange(0, 0, 255);
        axes[EvdevCodes.AxisHatX] = new EvdevAxisRange(0, -1, 1);
        axes[EvdevCodes.AxisHatY] = new EvdevAxisRange(0, -1, 1);

        return new EvdevDescription(0x054C, 0x0CE6, axes, Rumble: true);
    }

    /// <summary>A generic pad with sticks only: digital triggers (BTN_TL2/TR2), no rumble.</summary>
    public static EvdevDescription Generic()
    {
        var axes = new EvdevAxisRange[EvdevCodes.AxisCount];
        axes[EvdevCodes.AxisX] = new EvdevAxisRange(0, -127, 127);
        axes[EvdevCodes.AxisY] = new EvdevAxisRange(0, -127, 127);

        return new EvdevDescription(0x1234, 0x5678, axes, Rumble: false);
    }
}
