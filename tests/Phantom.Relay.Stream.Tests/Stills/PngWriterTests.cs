// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Imaging.Png;
using Phantom.Relay.Stream.Stills;

namespace Phantom.Relay.Stream.Tests.Stills;

public sealed class PngWriterTests
{
    [Fact]
    public void The_engines_reader_reads_back_every_pixel_written()
    {
        const int width = 37;
        const int height = 21;
        var rgb = new byte[width * height * PngWriter.BytesPerPixel];

        for (var index = 0; index < rgb.Length; index++)
        {
            rgb[index] = (byte)((index * 7) + (index / 5));
        }

        var image = PngReader.Read(Write(width, height, rgb));

        Assert.Equal((width, height), (image.Width, image.Height));
        Assert.Equal(rgb, Rgb(image.Data.Span));
    }

    [Fact]
    public void A_single_pixel_picture_round_trips()
    {
        var image = PngReader.Read(Write(1, 1, [200, 100, 50]));

        Assert.Equal([200, 100, 50, 255], image.Data.ToArray());
    }

    [Fact]
    public void The_file_starts_with_the_signature_and_an_ihdr_of_true_colour()
    {
        var file = Write(2, 3, new byte[2 * 3 * PngWriter.BytesPerPixel]);

        Assert.True(PngReader.IsPng(file));
        Assert.Equal("IHDR"u8.ToArray(), file[12..16]);
        Assert.Equal([0, 0, 0, 2, 0, 0, 0, 3, 8, 2, 0, 0, 0], file[16..29]);
        Assert.Equal("IEND"u8.ToArray(), file[^8..^4]);
    }

    [Fact]
    public void Too_few_bytes_for_the_size_are_refused()
    {
        using var output = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.Write(output, 2, 2, new byte[5]));
    }

    private static byte[] Write(int width, int height, byte[] rgb)
    {
        using var output = new MemoryStream();
        PngWriter.Write(output, width, height, rgb);

        return output.ToArray();
    }

    // The reader gives RGBA: the alpha dropped, every one opaque.
    private static byte[] Rgb(ReadOnlySpan<byte> rgba)
    {
        var rgb = new byte[rgba.Length / 4 * PngWriter.BytesPerPixel];

        for (var pixel = 0; pixel < rgba.Length / 4; pixel++)
        {
            Assert.Equal(255, rgba[(pixel * 4) + 3]);
            rgba.Slice(pixel * 4, PngWriter.BytesPerPixel).CopyTo(rgb.AsSpan(pixel * PngWriter.BytesPerPixel));
        }

        return rgb;
    }
}
