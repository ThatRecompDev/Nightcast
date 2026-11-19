// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Engine.Testing.Allocations;

namespace Phantom.Relay.Media.Windows.Tests;

public sealed class H264StreamColourTests
{
    [Fact]
    public void A_main_profile_set_without_vui_says_nothing_and_is_limited_range()
    {
        var sps = MainProfile(vui: false).ToUnit(7);

        var read = H264StreamColour.TryRead(sps, out var space, out var range);

        Assert.True(read);
        Assert.Null(space);
        Assert.Equal(VideoRange.Limited, range);
    }

    [Fact]
    public void The_vui_colour_description_gives_the_matrix_and_the_range()
    {
        var sps = VideoSignal(MainProfile(vui: true), fullRange: true, matrix: 6).ToUnit(7);

        _ = H264StreamColour.TryRead(sps, out var space, out var range);

        Assert.Equal(VideoColourSpace.Bt601, space);
        Assert.Equal(VideoRange.Full, range);
    }

    [Fact]
    public void A_video_signal_without_colour_description_gives_the_range_only()
    {
        var writer = MainProfile(vui: true)
            .Flag(false).Flag(false)
            .Flag(true).Bits(5, 3).Flag(true).Flag(false)
            .Flag(false).Flag(false);

        _ = H264StreamColour.TryRead(writer.ToUnit(7), out var space, out var range);

        Assert.Null(space);
        Assert.Equal(VideoRange.Full, range);
    }

    [Fact]
    public void A_high_profile_set_with_scaling_lists_and_cycle_order_counts_is_read_through()
    {
        var writer = new NalWriter()
            .Bits(100, 8).Bits(0, 8).Bits(51, 8).Ue(0)
            .Ue(1).Ue(0).Ue(0).Flag(false)
            .Flag(true);

        // Eight lists: the first ends after two deltas, the seventh (8x8) at once, the others absent.
        writer.Flag(true).Se(8).Se(-16);
        writer.Flag(false).Flag(false).Flag(false).Flag(false).Flag(false);
        writer.Flag(true).Se(-8);
        writer.Flag(false);

        // log2_max_frame_num, order count type 1 with a cycle of two, then the rest as the main profile's.
        writer.Ue(0).Ue(1).Flag(false).Se(-2).Se(3).Ue(2).Se(1).Se(-1);
        Rest(writer, vui: true);
        VideoSignal(writer, fullRange: false, matrix: 9);

        var read = H264StreamColour.TryRead(writer.ToUnit(7), out var space, out var range);

        Assert.True(read);
        Assert.Equal(VideoColourSpace.Bt2020, space);
        Assert.Equal(VideoRange.Limited, range);
    }

    [Fact]
    public void An_extended_sample_aspect_ratio_is_skipped()
    {
        var writer = MainProfile(vui: true)
            .Flag(true).Bits(255, 8).Bits(4, 16).Bits(3, 16)
            .Flag(false)
            .Flag(true).Bits(5, 3).Flag(false).Flag(true).Bits(1, 8).Bits(1, 8).Bits(1, 8)
            .Flag(false).Flag(false);

        _ = H264StreamColour.TryRead(writer.ToUnit(7), out var space, out _);

        Assert.Equal(VideoColourSpace.Bt709, space);
    }

    [Fact]
    public void Another_unit_or_a_cut_set_is_refused()
    {
        var sps = VideoSignal(MainProfile(vui: true), fullRange: true, matrix: 1).ToUnit(7);

        Assert.False(H264StreamColour.TryRead(MainProfile(vui: false).ToUnit(8), out _, out _));
        Assert.False(H264StreamColour.TryRead(sps.AsSpan(0, 6), out _, out _));
    }

    [Theory]
    [InlineData(0, VideoColourSpace.Rgb)]
    [InlineData(1, VideoColourSpace.Bt709)]
    [InlineData(5, VideoColourSpace.Bt601)]
    [InlineData(6, VideoColourSpace.Bt601)]
    [InlineData(7, VideoColourSpace.Smpte240)]
    [InlineData(9, VideoColourSpace.Bt2020)]
    [InlineData(10, VideoColourSpace.Bt2020)]
    public void Matrix_coefficients_name_the_engine_matrices(int matrix, VideoColourSpace expected)
    {
        Assert.Equal(expected, H264StreamColour.SpaceOf(matrix));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(12)]
    public void Unknown_matrices_are_left_to_the_default(int matrix)
    {
        Assert.Null(H264StreamColour.SpaceOf(matrix));
    }

    [Fact]
    public void An_unknown_matrix_is_drawn_as_bt709_in_the_stated_range()
    {
        Assert.Equal(new VideoColour(VideoColourSpace.Bt709, VideoRange.Full), H264StreamColour.Settle(null, VideoRange.Full));
        Assert.Equal(new VideoColour(VideoColourSpace.Bt601, VideoRange.Limited), H264StreamColour.Settle(VideoColourSpace.Bt601, VideoRange.Limited));
    }

    [Fact]
    public void Emulation_prevention_bytes_are_not_payload()
    {
        var reader = new RbspBitReader([0x00, 0x00, 0x03, 0x01, 0x80]);

        var value = reader.ReadBits(24);
        var stop = reader.ReadBit();

        Assert.Equal(1u, value);
        Assert.Equal(1, stop);
        Assert.False(reader.Overran);
    }

    [Fact]
    public void Reading_the_colour_allocates_nothing()
    {
        var sps = VideoSignal(MainProfile(vui: true), fullRange: false, matrix: 1).ToUnit(7);

        var report = AllocationMeter.Measure(() => H264StreamColour.TryRead(sps, out _, out _));

        Assert.Equal(0, report.BytesPerRun);
    }

    // The console's shape: Main profile, level 4.2, 2560 x 1440 (160 x 90 macroblocks), order count type 0.
    private static NalWriter MainProfile(bool vui)
    {
        var writer = new NalWriter().Bits(77, 8).Bits(0x40, 8).Bits(42, 8).Ue(0);
        writer.Ue(0).Ue(0).Ue(2);

        return Rest(writer, vui);
    }

    private static NalWriter Rest(NalWriter writer, bool vui)
    {
        return writer.Ue(1).Flag(false).Ue(159).Ue(89).Flag(true).Flag(true).Flag(false).Flag(vui);
    }

    // No aspect ratio, no overscan, a video signal with its colour description, then no more.
    private static NalWriter VideoSignal(NalWriter writer, bool fullRange, int matrix)
    {
        return writer.Flag(false).Flag(false)
            .Flag(true).Bits(5, 3).Flag(fullRange).Flag(true).Bits(1, 8).Bits(1, 8).Bits((uint)matrix, 8)
            .Flag(false).Flag(false);
    }
}
