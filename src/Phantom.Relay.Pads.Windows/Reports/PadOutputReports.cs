// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>The output report of each model (<see cref="DualSenseOutputReport"/>, <see cref="DualShock4OutputReport"/>).</summary>
public static class PadOutputReports
{
    /// <summary>The longest output report of any model: the Bluetooth ones.</summary>
    public const int MaxLength = DualSenseOutputReport.BluetoothLength;

    /// <summary>
    /// Writes a model's output report into the start of <paramref name="report"/> (at least <see cref="MaxLength"/>
    /// bytes) and answers its length; <paramref name="sequence"/> tags a DualSense's Bluetooth report.
    /// </summary>
    public static int Write(Span<byte> report, PadModel model, PadTransport transport, in PadOutput output, int sequence)
    {
        if (model == PadModel.DualShock4)
        {
            return DualShock4OutputReport.Write(report, transport, output);
        }

        return DualSenseOutputReport.Write(report, transport, output, sequence);
    }
}
