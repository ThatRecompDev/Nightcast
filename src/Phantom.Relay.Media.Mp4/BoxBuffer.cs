// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// Bytes written big-endian into a buffer that grows as needed and is used again (<see cref="Reset"/>), with boxes
/// opened (<see cref="Begin"/>: a size to fill in and the type) and closed (<see cref="End"/>: the size written once
/// the box's content is). One thread; after it has grown to a fragment's size nothing more is allocated.
/// </summary>
internal sealed class BoxBuffer
{
    /// <summary>A box's header: its 32-bit size and its type.</summary>
    public const int HeaderLength = 8;

    private const int VersionShift = 24;
    private const int Growth = 2;

    private byte[] _bytes;

    public BoxBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _bytes = new byte[capacity];
    }

    /// <summary>The bytes written so far.</summary>
    public int Length { get; private set; }

    /// <summary>What was written.</summary>
    public ReadOnlySpan<byte> Written => _bytes.AsSpan(0, Length);

    /// <summary>Forgets what was written; the buffer is kept.</summary>
    public void Reset()
    {
        Length = 0;
    }

    /// <summary>Opens a box of <paramref name="type"/>; its offset, for <see cref="End"/>.</summary>
    public int Begin(uint type)
    {
        var start = Length;
        U32(0);
        U32(type);

        return start;
    }

    /// <summary>Opens a full box (a version and 24 bits of flags after the header); its offset, for <see cref="End"/>.</summary>
    public int BeginFull(uint type, int version, uint flags)
    {
        var start = Begin(type);
        U32(((uint)version << VersionShift) | flags);

        return start;
    }

    /// <summary>Closes the box opened at <paramref name="start"/>: its size is what was written since.</summary>
    public void End(int start)
    {
        PatchU32(start, (uint)(Length - start));
    }

    public void U8(int value)
    {
        Reserve(sizeof(byte))[0] = (byte)value;
    }

    public void U16(int value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(Reserve(sizeof(ushort)), (ushort)value);
    }

    public void U32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(Reserve(sizeof(uint)), value);
    }

    public void I32(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(Reserve(sizeof(int)), value);
    }

    public void U64(ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(Reserve(sizeof(ulong)), value);
    }

    public void Bytes(ReadOnlySpan<byte> value)
    {
        value.CopyTo(Reserve(value.Length));
    }

    /// <summary><paramref name="count"/> zero bytes (reserved fields).</summary>
    public void Zeros(int count)
    {
        Reserve(count).Clear();
    }

    /// <summary>Writes <paramref name="value"/> over the four bytes at <paramref name="offset"/>.</summary>
    public void PatchU32(int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_bytes.AsSpan(offset, sizeof(uint)), value);
    }

    /// <summary>The next <paramref name="count"/> bytes, to be written by the caller; the length moves past them.</summary>
    public Span<byte> Reserve(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (Length + count > _bytes.Length)
        {
            Array.Resize(ref _bytes, Math.Max(_bytes.Length * Growth, Length + count));
        }

        var span = _bytes.AsSpan(Length, count);
        Length += count;

        return span;
    }

    /// <summary>Drops the bytes before <paramref name="offset"/>, moving the rest to the front.</summary>
    public void DropBefore(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Length);

        _bytes.AsSpan(offset, Length - offset).CopyTo(_bytes);
        Length -= offset;
    }
}
