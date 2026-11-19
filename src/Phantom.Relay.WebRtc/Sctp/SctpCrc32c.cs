// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Numerics;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// CRC-32C (Castagnoli, reflected polynomial 0x82F63B78), the checksum of every SCTP packet (RFC 9260 appendix B),
/// started and finished with all ones. The runtime's <see cref="BitOperations.Crc32C(uint, ulong)"/> uses the
/// processor's instruction where there is one (SSE 4.2, ARMv8) and a table elsewhere, so every target gives the same
/// value. Allocation-free; not a secure hash.
/// </summary>
public static class SctpCrc32c
{
    /// <summary>The register's starting value; <see cref="Finish"/> turns the register into the checksum.</summary>
    public const uint Seed = 0xFFFFFFFF;

    [HotPath]
    public static uint Hash(ReadOnlySpan<byte> data)
    {
        return Finish(Update(Seed, data));
    }

    /// <summary>Runs the register over <paramref name="data"/>, eight bytes at a time and then byte by byte.</summary>
    [HotPath]
    public static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        while (data.Length >= sizeof(ulong))
        {
            crc = BitOperations.Crc32C(crc, BinaryPrimitives.ReadUInt64LittleEndian(data));
            data = data[sizeof(ulong)..];
        }

        foreach (var value in data)
        {
            crc = BitOperations.Crc32C(crc, value);
        }

        return crc;
    }

    [HotPath]
    public static uint Finish(uint crc)
    {
        return crc ^ Seed;
    }
}
