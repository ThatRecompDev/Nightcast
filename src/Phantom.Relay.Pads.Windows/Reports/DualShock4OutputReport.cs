// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The DualShock 4's output report: report 0x05 over USB (32 bytes), report 0x11 over Bluetooth (78 bytes: a control
/// byte and an audio byte before the shared block, a <see cref="BluetoothOutputCrc"/> at the end). The shared block,
/// from its first byte:
/// <code>
/// 0 flags (1 motors, 2 light bar) | 1 flags | 2 reserved | 3 right (high) motor | 4 left (low) motor | 5 red | 6 green
/// | 7 blue | 8 flash on | 9 flash off
/// </code>
/// The layout is the one the public HID descriptors and the Linux hid-playstation documentation describe; it has not
/// been checked on a pad yet.
/// </summary>
public static class DualShock4OutputReport
{
    public const byte UsbReportId = 0x05;
    public const byte BluetoothReportId = 0x11;
    public const int UsbLength = 32;
    public const int BluetoothLength = 78;

    private const int UsbStart = 1;
    private const int BluetoothStart = 3;
    private const int BlockLength = 10;

    /// <summary>Bluetooth: the control byte says the report is a HID report closed by a CRC-32.</summary>
    private const int ControlOffset = 1;
    private const byte HidControl = 0x80;
    private const byte CrcControl = 0x40;

    private const int Flags0 = 0;
    private const int HighMotor = 3;
    private const int LowMotor = 4;
    private const int Red = 5;
    private const int Green = 6;
    private const int Blue = 7;

    private const byte Motors = 0x01;
    private const byte LightBar = 0x02;

    /// <summary>The report's length over a transport.</summary>
    public static int Length(PadTransport transport)
    {
        if (transport == PadTransport.Usb)
        {
            return UsbLength;
        }

        return BluetoothLength;
    }

    /// <summary>Writes the report into the start of <paramref name="report"/> and answers its length.</summary>
    public static int Write(Span<byte> report, PadTransport transport, in PadOutput output)
    {
        var length = Length(transport);
        var written = report[..length];
        written.Clear();

        if (transport == PadTransport.Usb)
        {
            written[0] = UsbReportId;
            Block(written.Slice(UsbStart, BlockLength), output);

            return length;
        }

        written[0] = BluetoothReportId;
        written[ControlOffset] = HidControl | CrcControl;
        Block(written.Slice(BluetoothStart, BlockLength), output);
        BluetoothOutputCrc.Append(written);

        return length;
    }

    private static void Block(Span<byte> block, in PadOutput output)
    {
        block[Flags0] = Motors;
        block[HighMotor] = output.HighMotor;
        block[LowMotor] = output.LowMotor;

        if (output.Light)
        {
            block[Flags0] |= LightBar;
            block[Red] = output.Red;
            block[Green] = output.Green;
            block[Blue] = output.Blue;
        }
    }
}
