// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Relay.Pads.Linux.Events;

namespace Phantom.Relay.Pads.Linux.Interop;

/// <summary>
/// A <c>struct ff_effect</c> of type <c>FF_RUMBLE</c> as <c>EVIOCSFF</c> takes it on a 64-bit kernel:
/// <code>
/// 0 type (u16) | 2 id (s16, -1 for a new effect; the kernel writes the id back) | 4 direction | 6 trigger (4 bytes) |
/// 10 replay length (u16, 0: until stopped) | 12 replay delay | 14 padding | 16 strong magnitude (u16) | 18 weak (u16)
/// </code>
/// 48 bytes in all (<see cref="EvdevRequests.EffectSize"/>). Strong is the low-frequency (large) motor, weak the high.
/// </summary>
internal static class RumbleEffect
{
    /// <summary>The id of an effect not uploaded yet.</summary>
    public const short NewEffect = -1;

    private const int TypeOffset = 0;
    private const int IdOffset = 2;
    private const int StrongOffset = 16;
    private const int WeakOffset = 18;

    /// <summary>Writes the effect into <paramref name="effect"/> (<see cref="EvdevRequests.EffectSize"/> bytes).</summary>
    public static void Write(Span<byte> effect, short id, ushort strong, ushort weak)
    {
        effect[..EvdevRequests.EffectSize].Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(effect[TypeOffset..], EvdevCodes.Rumble);
        BinaryPrimitives.WriteInt16LittleEndian(effect[IdOffset..], id);
        BinaryPrimitives.WriteUInt16LittleEndian(effect[StrongOffset..], strong);
        BinaryPrimitives.WriteUInt16LittleEndian(effect[WeakOffset..], weak);
    }

    /// <summary>The id the kernel gave the effect.</summary>
    public static short Id(ReadOnlySpan<byte> effect)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(effect[IdOffset..]);
    }

    /// <summary>The event that starts (<paramref name="play"/>) or stops the effect <paramref name="id"/>.</summary>
    public static InputEvent Playback(short id, bool play)
    {
        var value = 0;

        if (play)
        {
            value = 1;
        }

        return new InputEvent(EvdevCodes.ForceFeedback, (ushort)id, value);
    }
}
