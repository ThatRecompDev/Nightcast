// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// Copies an NV12 picture (a Y plane, then one plane of interleaved Cb, Cr pairs at half the size each way, both rows
/// <c>pitch</c> bytes apart, as a mapped D3D11 texture or a Media Foundation buffer holds it) into a
/// <see cref="VideoFrame"/>'s tightly packed Y, Cb and Cr planes. Luma rows are block copies; chroma is split sixteen
/// pairs at a time with vector narrowing where the CPU has it. Copying allocates nothing once the frame is large enough.
/// </summary>
public static class Nv12Planes
{
    private const int ChromaShift = 1;
    private const int BytesPerPair = 2;
    private const int BitsPerByte = 8;
    private const ushort LowByteMask = 0x00FF;

    /// <summary>
    /// Copies <paramref name="width"/> x <paramref name="height"/> from <paramref name="luma"/> and
    /// <paramref name="chroma"/> (the UV plane, from its first row) into <paramref name="target"/>.
    /// </summary>
    [HotPath]
    public static void CopyToFrame(ReadOnlySpan<byte> luma, ReadOnlySpan<byte> chroma, int pitch, int width, int height, VideoFrame target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var chromaWidth = VideoFrame.ChromaSide(width, ChromaShift);
        var chromaHeight = VideoFrame.ChromaSide(height, ChromaShift);
        ArgumentOutOfRangeException.ThrowIfLessThan(pitch, chromaWidth * BytesPerPair);
        ArgumentOutOfRangeException.ThrowIfLessThan(luma.Length, (pitch * (height - 1)) + width);
        ArgumentOutOfRangeException.ThrowIfLessThan(chroma.Length, (pitch * (chromaHeight - 1)) + (chromaWidth * BytesPerPair));

        _ = target.Prepare(width, height, chromaWidth, chromaHeight, alpha: false);
        CopyLuma(luma, pitch, width, height, target.Luma);

        var cb = target.Cb;
        var cr = target.Cr;

        for (var row = 0; row < chromaHeight; row++)
        {
            var pairs = chroma.Slice(row * pitch, chromaWidth * BytesPerPair);
            var offset = row * chromaWidth;
            Deinterleave(pairs, cb.Slice(offset, chromaWidth), cr.Slice(offset, chromaWidth));
        }
    }

    /// <summary>Splits Cb, Cr pairs into their own rows (<paramref name="cb"/>'s length is the number of pairs).</summary>
    [HotPath]
    public static void Deinterleave(ReadOnlySpan<byte> pairs, Span<byte> cb, Span<byte> cr)
    {
        var count = cb.Length;
        ArgumentOutOfRangeException.ThrowIfLessThan(pairs.Length, count * BytesPerPair);
        ArgumentOutOfRangeException.ThrowIfLessThan(cr.Length, count);

        var index = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            index = DeinterleaveVectors(pairs, cb, cr, count);
        }

        for (; index < count; index++)
        {
            cb[index] = pairs[index * BytesPerPair];
            cr[index] = pairs[(index * BytesPerPair) + 1];
        }
    }

    // Each step reads Vector128<byte>.Count pairs as two vectors of pairs (Cb low, Cr high) and narrows them apart.
    // The loads stay inside pairs: Deinterleave checked that it holds count pairs, and a step ends at or before count.
    [HotPath]
    private static int DeinterleaveVectors(ReadOnlySpan<byte> pairs, Span<byte> cb, Span<byte> cr, int count)
    {
        var step = Vector128<byte>.Count;
        ref var source = ref MemoryMarshal.GetReference(pairs);
        var mask = Vector128.Create(LowByteMask);
        var index = 0;

        for (; index + step <= count; index += step)
        {
            var offset = (nuint)(index * BytesPerPair);
            var first = Vector128.LoadUnsafe(ref source, offset).AsUInt16();
            var second = Vector128.LoadUnsafe(ref source, offset + (nuint)step).AsUInt16();

            var low = Vector128.Narrow(first & mask, second & mask);
            var high = Vector128.Narrow(Vector128.ShiftRightLogical(first, BitsPerByte), Vector128.ShiftRightLogical(second, BitsPerByte));
            low.CopyTo(cb[index..]);
            high.CopyTo(cr[index..]);
        }

        return index;
    }

    [HotPath]
    private static void CopyLuma(ReadOnlySpan<byte> luma, int pitch, int width, int height, Span<byte> target)
    {
        if (pitch == width)
        {
            luma[..(width * height)].CopyTo(target);

            return;
        }

        for (var row = 0; row < height; row++)
        {
            luma.Slice(row * pitch, width).CopyTo(target.Slice(row * width, width));
        }
    }
}
