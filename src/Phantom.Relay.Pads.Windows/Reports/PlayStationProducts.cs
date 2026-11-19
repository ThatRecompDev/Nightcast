// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The USB product ids of the pads read here, under Sony's vendor id (<see cref="GamepadVendors.Sony"/>). A pad reports
/// the same ids over Bluetooth.
/// </summary>
public static class PlayStationProducts
{
    /// <summary>The DualSense (CFI-ZCT1).</summary>
    public const ushort DualSense = 0x0CE6;

    /// <summary>The DualSense Edge (CFI-ZCP1).</summary>
    public const ushort DualSenseEdge = 0x0DF2;

    /// <summary>The first DualShock 4 (CUH-ZCT1).</summary>
    public const ushort DualShock4 = 0x05C4;

    /// <summary>The second DualShock 4 (CUH-ZCT2), with the light bar showing through the touchpad.</summary>
    public const ushort DualShock4Second = 0x09CC;

    /// <summary>The model of a vendor and product id pair; false for any other pad or device.</summary>
    public static bool TryModel(ushort vendor, ushort product, out PadModel model)
    {
        model = PadModel.DualSense;

        if (vendor != GamepadVendors.Sony)
        {
            return false;
        }

        switch (product)
        {
            case DualSense:
                return true;

            case DualSenseEdge:
                model = PadModel.DualSenseEdge;

                return true;

            case DualShock4:
            case DualShock4Second:
                model = PadModel.DualShock4;

                return true;

            default:
                return false;
        }
    }
}
