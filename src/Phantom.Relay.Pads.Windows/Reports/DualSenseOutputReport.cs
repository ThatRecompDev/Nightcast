// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The DualSense's output report: report 0x02 over USB (48 bytes), report 0x31 over Bluetooth (78 bytes: a sequence
/// tag and a fixed tag before the shared block, a <see cref="BluetoothOutputCrc"/> at the end). The shared block, from
/// its first byte:
/// <code>
/// 0 flags (1 rumble as the older pads, 2 haptics select) | 1 flags (4 light bar) | 2 right (high) motor | 3 left (low)
/// motor | 4-37 audio, LEDs, power, reserved | 38 flags (2 light-bar setup) | 41 light-bar setup (2 fade the pad's own
/// light out) | 42 LED brightness | 43 player LEDs | 44 red | 45 green | 46 blue
/// </code>
/// The motors are emulated by the haptic actuators, as on a PS5 in a PS4 game. The layout is the one the public HID
/// descriptors and the Linux hid-playstation documentation describe; it has not been checked on a pad yet.
/// </summary>
public static class DualSenseOutputReport
{
    public const byte UsbReportId = 0x02;
    public const byte BluetoothReportId = 0x31;
    public const int UsbLength = UsbStart + BlockLength;
    public const int BluetoothLength = 78;

    private const int UsbStart = 1;
    private const int BluetoothStart = 3;
    private const int BlockLength = 47;

    /// <summary>Bluetooth: the sequence tag (a 4-bit counter in the high nibble) and the fixed tag after it.</summary>
    private const int SequenceOffset = 1;
    private const int TagOffset = 2;
    private const byte Tag = 0x10;
    private const int SequenceShift = 4;
    private const int SequenceMask = 0x0F;

    private const int Flags0 = 0;
    private const int Flags1 = 1;
    private const int HighMotor = 2;
    private const int LowMotor = 3;
    private const int Flags2 = 38;
    private const int LightSetup = 41;
    private const int Red = 44;
    private const int Green = 45;
    private const int Blue = 46;

    private const byte CompatibleVibration = 0x01;
    private const byte HapticsSelect = 0x02;
    private const byte LightBarControl = 0x04;
    private const byte LightSetupControl = 0x02;
    private const byte LightOut = 0x02;

    /// <summary>The report's length over a transport.</summary>
    public static int Length(PadTransport transport)
    {
        if (transport == PadTransport.Usb)
        {
            return UsbLength;
        }

        return BluetoothLength;
    }

    /// <summary>
    /// Writes the report into the start of <paramref name="report"/> and answers its length. Over Bluetooth
    /// <paramref name="sequence"/> (its low four bits) tags it, and the check closes it.
    /// </summary>
    public static int Write(Span<byte> report, PadTransport transport, in PadOutput output, int sequence)
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
        written[SequenceOffset] = (byte)((sequence & SequenceMask) << SequenceShift);
        written[TagOffset] = Tag;
        Block(written.Slice(BluetoothStart, BlockLength), output);
        BluetoothOutputCrc.Append(written);

        return length;
    }

    private static void Block(Span<byte> block, in PadOutput output)
    {
        block[Flags0] = CompatibleVibration | HapticsSelect;
        block[HighMotor] = output.HighMotor;
        block[LowMotor] = output.LowMotor;

        if (output.Light)
        {
            block[Flags1] = LightBarControl;
            block[Red] = output.Red;
            block[Green] = output.Green;
            block[Blue] = output.Blue;
        }

        if (output.ReleaseLight)
        {
            block[Flags2] = LightSetupControl;
            block[LightSetup] = LightOut;
        }
    }
}
