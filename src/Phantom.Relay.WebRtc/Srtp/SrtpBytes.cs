// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>The exclusive-or steps SRTP's IVs, keystreams and tags are made of.</summary>
public static class SrtpBytes
{
    /// <summary>Exclusive-ors <paramref name="mask"/> into the start of <paramref name="destination"/>.</summary>
    [HotPath]
    public static void Xor(Span<byte> destination, ReadOnlySpan<byte> mask)
    {
        for (int i = 0; i < mask.Length; i++)
        {
            destination[i] ^= mask[i];
        }
    }

    /// <summary>Exclusive-ors a 32-bit big-endian word into the start of <paramref name="destination"/>.</summary>
    [HotPath]
    public static void XorWord(Span<byte> destination, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination, BinaryPrimitives.ReadUInt32BigEndian(destination) ^ value);
    }
}
