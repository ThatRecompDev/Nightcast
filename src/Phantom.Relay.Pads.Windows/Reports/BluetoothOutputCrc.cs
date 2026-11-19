// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core.Hashing;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The check a PlayStation pad wants at the end of a Bluetooth output report: the CRC-32 (zlib's,
/// <see cref="Crc32"/>) of the Bluetooth HID header byte for an output report (0xA2) followed by every byte of the
/// report before the check, written little-endian in the report's last four bytes. A pad drops a report whose check
/// does not match.
/// </summary>
public static class BluetoothOutputCrc
{
    /// <summary>The bytes of the check at the report's end.</summary>
    public const int Length = sizeof(uint);

    /// <summary>The Bluetooth HID transaction header of an output report (DATA, output), which the pad hashes first.</summary>
    public const byte Header = 0xA2;

    private const uint AllOnes = 0xFFFFFFFF;

    /// <summary>The check of <paramref name="report"/> without its last four bytes.</summary>
    public static uint Compute(ReadOnlySpan<byte> report)
    {
        Span<byte> header = stackalloc byte[1];
        header[0] = Header;
        var crc = Crc32.Update(AllOnes, header);
        crc = Crc32.Update(crc, report[..^Length]);

        return crc ^ AllOnes;
    }

    /// <summary>Writes the check into the report's last four bytes.</summary>
    public static void Append(Span<byte> report)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(report[^Length..], Compute(report));
    }
}
