// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// Reads SCTP packets: the common header, the checksum, and the chunks and parameters in turn. Allocation-free; the
/// spans point into the caller's packet.
/// </summary>
public static class SctpPacket
{
    private const int LengthOffset = 2;
    private const int ChunkFlagsOffset = 1;

    /// <summary>True when the packet holds a common header and its checksum matches its bytes.</summary>
    [HotPath]
    public static bool HasValidChecksum(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < SctpFormat.CommonHeaderLength)
        {
            return false;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(packet[SctpFormat.ChecksumOffset..]) == Checksum(packet);
    }

    /// <summary>The CRC-32C of the packet with its checksum field taken as zero, as RFC 9260 appendix B computes it.</summary>
    [HotPath]
    public static uint Checksum(ReadOnlySpan<byte> packet)
    {
        uint crc = SctpCrc32c.Update(SctpCrc32c.Seed, packet[..SctpFormat.ChecksumOffset]);
        crc = SctpCrc32c.Update(crc, ZeroChecksum);
        crc = SctpCrc32c.Update(crc, packet[SctpFormat.CommonHeaderLength..]);

        return SctpCrc32c.Finish(crc);
    }

    public static ushort SourcePort(ReadOnlySpan<byte> packet)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(packet);
    }

    public static ushort DestinationPort(ReadOnlySpan<byte> packet)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(packet[SctpFormat.DestinationPortOffset..]);
    }

    public static uint VerificationTag(ReadOnlySpan<byte> packet)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(packet[SctpFormat.VerificationTagOffset..]);
    }

    /// <summary>
    /// The chunk at <paramref name="offset"/>, which then moves past its padding; false at the end of the packet or at
    /// a chunk whose length does not fit it.
    /// </summary>
    [HotPath]
    public static bool TryReadChunk(ReadOnlySpan<byte> packet, scoped ref int offset, out byte type, out byte flags, out ReadOnlySpan<byte> value)
    {
        type = 0;
        flags = 0;
        value = default;

        if (offset + SctpFormat.ChunkHeaderLength > packet.Length)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + LengthOffset)..]);

        if (length < SctpFormat.ChunkHeaderLength || offset + length > packet.Length)
        {
            return false;
        }

        type = packet[offset];
        flags = packet[offset + ChunkFlagsOffset];
        value = packet.Slice(offset + SctpFormat.ChunkHeaderLength, length - SctpFormat.ChunkHeaderLength);
        offset += Padded(length);

        return true;
    }

    /// <summary>The parameter at <paramref name="offset"/> of a chunk's parameter list, which then moves past it.</summary>
    [HotPath]
    public static bool TryReadParameter(ReadOnlySpan<byte> parameters, scoped ref int offset, out ushort type, out ReadOnlySpan<byte> value)
    {
        type = 0;
        value = default;

        if (offset + SctpFormat.ParameterHeaderLength > parameters.Length)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadUInt16BigEndian(parameters[(offset + LengthOffset)..]);

        if (length < SctpFormat.ParameterHeaderLength || offset + length > parameters.Length)
        {
            return false;
        }

        type = BinaryPrimitives.ReadUInt16BigEndian(parameters[offset..]);
        value = parameters.Slice(offset + SctpFormat.ParameterHeaderLength, length - SctpFormat.ParameterHeaderLength);
        offset += Padded(length);

        return true;
    }

    /// <summary>A length rounded up to the next multiple of four, as chunks and parameters are padded.</summary>
    public static int Padded(int length)
    {
        return (length + SctpFormat.Alignment - 1) & ~(SctpFormat.Alignment - 1);
    }

    private static ReadOnlySpan<byte> ZeroChecksum => [0, 0, 0, 0];
}
