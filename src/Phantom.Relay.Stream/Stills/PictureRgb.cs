// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Stills;

/// <summary>
/// A decoded picture's Y, Cb and Cr planes (4:2:0, or 4:4:4) as 8-bit RGB, the way the renderer's shader draws it: the
/// engine's rows for the picture's colour (<see cref="YuvConversion.Rows"/>: BT.709 limited range unless the stream
/// says otherwise), each chroma sample spread over the pixels it covers, rounded and clamped. For a screenshot, on a
/// worker thread.
/// </summary>
public static class PictureRgb
{
    private const float Half = 0.5f;

    /// <summary>The bytes <paramref name="picture"/> takes as RGB.</summary>
    public static int Size(VideoFrame picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        return picture.Width * picture.Height * PngWriter.BytesPerPixel;
    }

    /// <summary>Converts <paramref name="picture"/> into <paramref name="rgb"/> (<see cref="Size"/> bytes, rows top first).</summary>
    public static void Convert(VideoFrame picture, Span<byte> rgb)
    {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentOutOfRangeException.ThrowIfLessThan(rgb.Length, Size(picture));

        var rows = YuvConversion.Rows(picture.Colour);
        var shiftX = Shift(picture.Width, picture.ChromaWidth);
        var shiftY = Shift(picture.Height, picture.ChromaHeight);

        for (var y = 0; y < picture.Height; y++)
        {
            var luma = picture.Luma.Slice(y * picture.Width, picture.Width);
            var chromaRow = (y >> shiftY) * picture.ChromaWidth;
            var cb = picture.Cb.Slice(chromaRow, picture.ChromaWidth);
            var cr = picture.Cr.Slice(chromaRow, picture.ChromaWidth);
            ConvertRow(rows, luma, cb, cr, shiftX, rgb.Slice(y * picture.Width * PngWriter.BytesPerPixel));
        }
    }

    // A chroma plane half the picture's side is subsampled (shift 1); one as large is not (shift 0).
    private static int Shift(int side, int chromaSide)
    {
        if (chromaSide < side)
        {
            return 1;
        }

        return 0;
    }

    private static void ConvertRow(YuvRows rows, ReadOnlySpan<byte> luma, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr, int shiftX, Span<byte> rgb)
    {
        var output = 0;

        for (var x = 0; x < luma.Length; x++)
        {
            var chroma = x >> shiftX;
            var sample = new Vector4(luma[x], cb[chroma], cr[chroma], YuvConversion.SampleMax) / YuvConversion.SampleMax;
            rgb[output++] = Channel(Vector4.Dot(rows.Red, sample));
            rgb[output++] = Channel(Vector4.Dot(rows.Green, sample));
            rgb[output++] = Channel(Vector4.Dot(rows.Blue, sample));
        }
    }

    private static byte Channel(float value)
    {
        return (byte)Math.Clamp((value * YuvConversion.SampleMax) + Half, 0f, YuvConversion.SampleMax);
    }
}
