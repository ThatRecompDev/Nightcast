// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>The input reports of each model, and what a model needs to send them in full over Bluetooth.</summary>
public static class PadInputReports
{
    /// <summary>Reads a model's full input report over a transport (<see cref="DualSenseInputReport"/>, <see cref="DualShock4InputReport"/>).</summary>
    public static bool TryRead(PadModel model, PadTransport transport, ReadOnlySpan<byte> report, out GamepadState state, out PadMotion motion)
    {
        if (model == PadModel.DualShock4)
        {
            return DualShock4InputReport.TryRead(report, transport, out state, out motion);
        }

        return DualSenseInputReport.TryRead(report, transport, out state, out motion);
    }

    /// <summary>The feature report to read once on Bluetooth before the pad sends its full input report.</summary>
    public static byte BluetoothModeFeatureReport(PadModel model)
    {
        if (model == PadModel.DualShock4)
        {
            return DualShock4InputReport.BluetoothModeFeatureReport;
        }

        return DualSenseInputReport.BluetoothModeFeatureReport;
    }
}
