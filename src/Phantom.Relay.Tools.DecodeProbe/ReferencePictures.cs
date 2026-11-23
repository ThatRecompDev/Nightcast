// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Tools.DecodeProbe;

/// <summary>
/// A reference decode of the same sample as raw 4:2:0 planes (FFmpeg's software decoder:
/// <c>ffmpeg -i sample.h264 -f rawvideo -pix_fmt yuv420p reference.yuv</c>), picture after picture, against which each
/// decoded picture is compared. H.264 decoding is exact, so a correct decoder differs from it by nothing.
/// </summary>
internal sealed class ReferencePictures(byte[] pictures)
{
    // Cb and Cr.
    private const int ChromaPlanes = 2;

    /// <summary>The largest difference of any sample of picture <paramref name="index"/> from the reference's; -1 when the reference has no such picture.</summary>
    public int Difference(int index, VideoFrame picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        var luma = picture.Width * picture.Height;
        var chroma = picture.ChromaWidth * picture.ChromaHeight;
        var size = luma + (chroma * ChromaPlanes);
        var start = (long)index * size;

        if (start + size > pictures.Length)
        {
            return -1;
        }

        var reference = pictures.AsSpan((int)start, size);
        var worst = Largest(reference[..luma], picture.Luma);
        worst = Math.Max(worst, Largest(reference.Slice(luma, chroma), picture.Cb));

        return Math.Max(worst, Largest(reference.Slice(luma + chroma, chroma), picture.Cr));
    }

    private static int Largest(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        var worst = 0;

        for (var index = 0; index < expected.Length; index++)
        {
            worst = Math.Max(worst, Math.Abs(expected[index] - actual[index]));
        }

        return worst;
    }
}
