// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Imaging.Png;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Stream.Stills;

namespace Phantom.Relay.Stream.Tests.Stills;

public sealed class PictureRgbTests
{
    [Theory]
    [InlineData(16, 128, 128, 0, 0, 0)]
    [InlineData(235, 128, 128, 255, 255, 255)]
    [InlineData(126, 128, 128, 128, 128, 128)]
    public void Limited_range_bt709_greys_map_to_full_range(byte luma, byte cb, byte cr, byte red, byte green, byte blue)
    {
        var rgb = Convert(Picture(2, 2, luma, cb, cr, VideoColour.Default));

        Assert.Equal([red, green, blue], rgb[..3]);
    }

    [Fact]
    public void Bt709_limited_red_is_red()
    {
        // BT.709's 75 % red bar is about Y 51, Cb 109, Cr 212 in limited range; full red is Y 63, Cb 102, Cr 240.
        var rgb = Convert(Picture(2, 2, 63, 102, 240, VideoColour.Default));

        Assert.InRange(rgb[0], 250, 255);
        Assert.InRange(rgb[1], 0, 5);
        Assert.InRange(rgb[2], 0, 5);
    }

    [Fact]
    public void Each_chroma_sample_covers_its_two_by_two_pixels()
    {
        var picture = Picture(4, 2, 126, 128, 128, VideoColour.Default);
        picture.Cr[1] = 240;

        var rgb = Convert(picture);

        Assert.Equal(rgb[0], rgb[3]);
        Assert.True(rgb[2 * 3] > rgb[0]);
        Assert.Equal(rgb[2 * 3], rgb[((1 * 4) + 3) * 3]);
    }

    [Fact]
    public void A_picture_becomes_a_png_the_engine_reads()
    {
        var picture = Picture(6, 4, 235, 128, 128, VideoColour.Default);
        using var output = new MemoryStream();

        StillImage.WritePng(picture, output);
        var image = PngReader.Read(output.ToArray());

        Assert.Equal((6, 4), (image.Width, image.Height));
        Assert.All(image.Data.ToArray(), value => Assert.Equal(255, value));
    }

    private static byte[] Convert(VideoFrame picture)
    {
        var rgb = new byte[PictureRgb.Size(picture)];
        PictureRgb.Convert(picture, rgb);

        return rgb;
    }

    private static VideoFrame Picture(int width, int height, byte luma, byte cb, byte cr, VideoColour colour)
    {
        var picture = new VideoFrame();
        picture.Prepare(width, height, VideoFrame.ChromaSide(width, 1), VideoFrame.ChromaSide(height, 1), alpha: false);
        picture.Luma.Fill(luma);
        picture.Cb.Fill(cb);
        picture.Cr.Fill(cr);
        picture.Colour = colour;

        return picture;
    }
}
