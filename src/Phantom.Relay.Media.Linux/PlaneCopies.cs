// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows;

namespace Phantom.Relay.Media.Linux;

/// <summary>
/// Copies a decoded 4:2:0 picture as libavcodec leaves it (each plane with its own pitch) into a
/// <see cref="VideoFrame"/>'s tightly packed Y, Cb and Cr planes, row by row: NV12 (a VA-API surface read back: Y,
/// then interleaved Cb, Cr pairs, split by <see cref="Nv12Planes.Deinterleave"/>) or three planes (the software
/// decoder's). Allocates nothing once the frame is large enough.
/// </summary>
internal static class PlaneCopies
{
    private const int ChromaShift = 1;
    private const int BytesPerPair = 2;

    /// <summary>How many bytes a plane of <paramref name="rows"/> rows <paramref name="pitch"/> apart spans, its last row <paramref name="width"/> long.</summary>
    public static int Extent(int pitch, int width, int rows)
    {
        return (pitch * (rows - 1)) + width;
    }

    /// <summary>The chroma plane's bytes a row of an NV12 picture <paramref name="width"/> wide holds (its Cb, Cr pairs).</summary>
    public static int Nv12ChromaRow(int width)
    {
        return VideoFrame.ChromaSide(width, ChromaShift) * BytesPerPair;
    }

    /// <summary>The chroma columns of a picture <paramref name="width"/> wide.</summary>
    public static int ChromaColumns(int width)
    {
        return VideoFrame.ChromaSide(width, ChromaShift);
    }

    /// <summary>The chroma rows of a picture <paramref name="height"/> high.</summary>
    public static int ChromaRows(int height)
    {
        return VideoFrame.ChromaSide(height, ChromaShift);
    }

    [HotPath]
    public static void CopyNv12(ReadOnlySpan<byte> luma, int lumaPitch, ReadOnlySpan<byte> chroma, int chromaPitch, int width, int height, VideoFrame target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var chromaWidth = VideoFrame.ChromaSide(width, ChromaShift);
        var chromaHeight = ChromaRows(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(chromaPitch, chromaWidth * BytesPerPair);
        ArgumentOutOfRangeException.ThrowIfLessThan(chroma.Length, Extent(chromaPitch, chromaWidth * BytesPerPair, chromaHeight));

        _ = target.Prepare(width, height, chromaWidth, chromaHeight, alpha: false);
        CopyPlane(luma, lumaPitch, width, height, target.Luma);

        var cb = target.Cb;
        var cr = target.Cr;

        for (var row = 0; row < chromaHeight; row++)
        {
            var offset = row * chromaWidth;
            Nv12Planes.Deinterleave(chroma.Slice(row * chromaPitch, chromaWidth * BytesPerPair), cb.Slice(offset, chromaWidth), cr.Slice(offset, chromaWidth));
        }
    }

    [HotPath]
    public static void CopyPlanar(ReadOnlySpan<byte> luma, int lumaPitch, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr, int chromaPitch, int width, int height, VideoFrame target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var chromaWidth = VideoFrame.ChromaSide(width, ChromaShift);
        var chromaHeight = ChromaRows(height);
        _ = target.Prepare(width, height, chromaWidth, chromaHeight, alpha: false);

        CopyPlane(luma, lumaPitch, width, height, target.Luma);
        CopyPlane(cb, chromaPitch, chromaWidth, chromaHeight, target.Cb);
        CopyPlane(cr, chromaPitch, chromaWidth, chromaHeight, target.Cr);
    }

    [HotPath]
    private static void CopyPlane(ReadOnlySpan<byte> source, int pitch, int width, int rows, Span<byte> target)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pitch, width);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.Length, Extent(pitch, width, rows));

        if (pitch == width)
        {
            source[..(width * rows)].CopyTo(target);

            return;
        }

        for (var row = 0; row < rows; row++)
        {
            source.Slice(row * pitch, width).CopyTo(target.Slice(row * width, width));
        }
    }
}
