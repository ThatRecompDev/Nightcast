// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// Builds one SCTP packet at a time in a buffer it owns: the common header, then chunks, each padded to four bytes,
/// then the checksum. Made once by its owner and reused for every packet, so building allocates nothing.
/// </summary>
public sealed class SctpPacketBuilder
{
    private const int LengthOffset = 2;
    private const int ChunkFlagsOffset = 1;

    private readonly byte[] _buffer;
    private int _length;

    public SctpPacketBuilder(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, SctpFormat.CommonHeaderLength + SctpFormat.ChunkHeaderLength);

        _buffer = new byte[maxLength];
    }

    public int MaxLength => _buffer.Length;

    /// <summary>The bytes written so far.</summary>
    public int Length => _length;

    /// <summary>True once a chunk follows the common header.</summary>
    public bool HasChunks => _length > SctpFormat.CommonHeaderLength;

    /// <summary>Starts a packet, dropping whatever was built before.</summary>
    [HotPath]
    public void Begin(ushort sourcePort, ushort destinationPort, uint verificationTag)
    {
        var header = _buffer.AsSpan(0, SctpFormat.CommonHeaderLength);
        BinaryPrimitives.WriteUInt16BigEndian(header, sourcePort);
        BinaryPrimitives.WriteUInt16BigEndian(header[SctpFormat.DestinationPortOffset..], destinationPort);
        BinaryPrimitives.WriteUInt32BigEndian(header[SctpFormat.VerificationTagOffset..], verificationTag);
        BinaryPrimitives.WriteUInt32LittleEndian(header[SctpFormat.ChecksumOffset..], 0);
        _length = SctpFormat.CommonHeaderLength;
    }

    /// <summary>True when a chunk with a value of <paramref name="valueLength"/> bytes still fits the packet.</summary>
    [HotPath]
    public bool Fits(int valueLength)
    {
        return _length + SctpPacket.Padded(SctpFormat.ChunkHeaderLength + valueLength) <= _buffer.Length;
    }

    /// <summary>
    /// Appends a chunk header and returns the room for its value, which the caller fills; the padding after it is
    /// zeroed. The chunk must fit (<see cref="Fits"/>).
    /// </summary>
    [HotPath]
    public Span<byte> AddChunk(byte type, byte flags, int valueLength)
    {
        if (!Fits(valueLength))
        {
            throw new InvalidOperationException("the chunk does not fit the packet");
        }

        int length = SctpFormat.ChunkHeaderLength + valueLength;
        int padded = SctpPacket.Padded(length);
        var chunk = _buffer.AsSpan(_length, padded);
        chunk[0] = type;
        chunk[ChunkFlagsOffset] = flags;
        BinaryPrimitives.WriteUInt16BigEndian(chunk[LengthOffset..], (ushort)length);

        chunk[length..].Clear();
        _length += padded;

        return chunk.Slice(SctpFormat.ChunkHeaderLength, valueLength);
    }

    /// <summary>Writes the checksum and returns the finished packet, valid until the next <see cref="Begin"/>.</summary>
    [HotPath]
    public ReadOnlySpan<byte> Seal()
    {
        var packet = _buffer.AsSpan(0, _length);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[SctpFormat.ChecksumOffset..], SctpPacket.Checksum(packet));

        return packet;
    }

    /// <summary>Writes a parameter (type, length, value, padding) and returns the bytes it takes.</summary>
    [HotPath]
    public static int WriteParameter(Span<byte> destination, ushort type, ReadOnlySpan<byte> value)
    {
        int length = SctpFormat.ParameterHeaderLength + value.Length;
        int padded = SctpPacket.Padded(length);
        BinaryPrimitives.WriteUInt16BigEndian(destination, type);
        BinaryPrimitives.WriteUInt16BigEndian(destination[LengthOffset..], (ushort)length);
        value.CopyTo(destination[SctpFormat.ParameterHeaderLength..]);
        destination[length..padded].Clear();

        return padded;
    }

    /// <summary>The bytes a parameter with a value of <paramref name="valueLength"/> bytes takes, padding included.</summary>
    public static int ParameterLength(int valueLength)
    {
        return SctpPacket.Padded(SctpFormat.ParameterHeaderLength + valueLength);
    }
}
