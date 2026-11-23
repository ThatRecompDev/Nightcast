// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.Pads.Linux.Events;

/// <summary>
/// One <c>struct input_event</c> as an evdev device is read and written with on 64-bit Linux (x86-64 and arm64):
/// <code>
/// 0 time (struct timeval: two 64-bit longs) | 16 type (u16) | 18 code (u16) | 20 value (s32)
/// </code>
/// 24 bytes, in the machine's order (little-endian on both). The time is not read; a written event leaves it zero,
/// which the kernel fills in.
/// </summary>
internal readonly record struct InputEvent(ushort Type, ushort Code, int Value)
{
    /// <summary>The size of one event on a 64-bit kernel.</summary>
    public const int Size = 24;

    private const int TypeOffset = 16;
    private const int CodeOffset = 18;
    private const int ValueOffset = 20;

    /// <summary>The event in the first <see cref="Size"/> bytes of <paramref name="bytes"/>.</summary>
    [HotPath]
    public static InputEvent Read(ReadOnlySpan<byte> bytes)
    {
        var type = BinaryPrimitives.ReadUInt16LittleEndian(bytes[TypeOffset..]);
        var code = BinaryPrimitives.ReadUInt16LittleEndian(bytes[CodeOffset..]);
        var value = BinaryPrimitives.ReadInt32LittleEndian(bytes[ValueOffset..]);

        return new InputEvent(type, code, value);
    }

    /// <summary>Writes the event into the first <see cref="Size"/> bytes of <paramref name="bytes"/>, its time zero.</summary>
    public void Write(Span<byte> bytes)
    {
        bytes[..TypeOffset].Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[TypeOffset..], Type);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[CodeOffset..], Code);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[ValueOffset..], Value);
    }
}
