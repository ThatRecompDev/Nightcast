// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Engine.Testing.Allocations;

namespace Phantom.Relay.Media.Windows.Tests;

public sealed class Nv12PlanesTests
{
    [Fact]
    public void An_odd_picture_in_padded_rows_lands_in_tight_planes()
    {
        // 5 x 3: chroma 3 x 2. Rows are 8 bytes apart; padding is 0xEE and must not be copied.
        byte[] luma =
        [
            10, 11, 12, 13, 14, 0xEE, 0xEE, 0xEE,
            20, 21, 22, 23, 24, 0xEE, 0xEE, 0xEE,
            30, 31, 32, 33, 34, 0xEE, 0xEE, 0xEE,
        ];
        byte[] chroma =
        [
            100, 200, 101, 201, 102, 202, 0xEE, 0xEE,
            110, 210, 111, 211, 112, 212, 0xEE, 0xEE,
        ];
        var frame = new VideoFrame();

        Nv12Planes.CopyToFrame(luma, chroma, 8, 5, 3, frame);

        Assert.Equal((5, 3, 3, 2), (frame.Width, frame.Height, frame.ChromaWidth, frame.ChromaHeight));
        Assert.Equal([10, 11, 12, 13, 14, 20, 21, 22, 23, 24, 30, 31, 32, 33, 34], frame.Luma.ToArray());
        Assert.Equal([100, 101, 102, 110, 111, 112], frame.Cb.ToArray());
        Assert.Equal([200, 201, 202, 210, 211, 212], frame.Cr.ToArray());
        Assert.False(frame.HasAlpha);
    }

    [Fact]
    public void Rows_without_padding_are_copied_in_one_piece()
    {
        byte[] luma = [1, 2, 3, 4, 5, 6, 7, 8];
        byte[] chroma = [9, 10, 11, 12];
        var frame = new VideoFrame();

        Nv12Planes.CopyToFrame(luma, chroma, 4, 4, 2, frame);

        Assert.Equal(luma, frame.Luma.ToArray());
        Assert.Equal([9, 11], frame.Cb.ToArray());
        Assert.Equal([10, 12], frame.Cr.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(40)]
    [InlineData(1280)]
    public void Pairs_split_alike_on_the_vector_path_and_its_tail(int count)
    {
        var pairs = new byte[count * 2];

        for (var index = 0; index < count; index++)
        {
            pairs[index * 2] = (byte)index;
            pairs[(index * 2) + 1] = (byte)(255 - index);
        }

        var cb = new byte[count];
        var cr = new byte[count];

        Nv12Planes.Deinterleave(pairs, cb, cr);

        for (var index = 0; index < count; index++)
        {
            Assert.Equal((byte)index, cb[index]);
            Assert.Equal((byte)(255 - index), cr[index]);
        }
    }

    [Fact]
    public void A_buffer_too_small_for_the_picture_is_refused()
    {
        var frame = new VideoFrame();

        Assert.Throws<ArgumentOutOfRangeException>(() => Nv12Planes.CopyToFrame(new byte[15], new byte[8], 8, 8, 2, frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => Nv12Planes.CopyToFrame(new byte[16], new byte[7], 8, 8, 2, frame));
    }

    [Fact]
    public void Copying_into_a_large_enough_frame_allocates_nothing()
    {
        var luma = new byte[64 * 36];
        var chroma = new byte[64 * 18];
        var frame = new VideoFrame();
        Nv12Planes.CopyToFrame(luma, chroma, 64, 64, 36, frame);

        var report = AllocationMeter.Measure(() => Nv12Planes.CopyToFrame(luma, chroma, 64, 64, 36, frame));

        Assert.Equal(0, report.BytesPerRun);
    }
}
