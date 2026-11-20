// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class RtpTimelineTests
{
    [Fact]
    public void The_first_unit_is_at_zero_and_a_60_fps_step_is_a_sixtieth_of_a_second()
    {
        var timeline = new RtpTimeline();

        var first = timeline.Next(123_456);
        var second = timeline.Next(123_456 + 1500);

        Assert.Equal(0, first);
        Assert.Equal(166_666, second);
    }

    [Fact]
    public void A_second_of_the_90_khz_clock_is_ten_million_units_exactly()
    {
        Assert.Equal(10_000_000, RtpTimeline.ToUnits(90_000));
    }

    [Fact]
    public void Times_are_counted_from_the_first_so_rounding_never_gathers()
    {
        var timeline = new RtpTimeline();
        var time = 0L;

        for (var unit = 0u; unit <= 3600; unit++)
        {
            time = timeline.Next(unit * 1500);
        }

        Assert.Equal(600_000_000, time);
    }

    [Fact]
    public void The_clock_wrapping_past_32_bits_keeps_counting_forward()
    {
        var timeline = new RtpTimeline();
        _ = timeline.Next(uint.MaxValue - 499);

        var after = timeline.Next(1000);

        Assert.Equal(RtpTimeline.ToUnits(1500), after);
    }

    [Fact]
    public void A_unit_a_little_out_of_order_is_a_little_earlier()
    {
        var timeline = new RtpTimeline();
        _ = timeline.Next(9000);
        _ = timeline.Next(12_000);

        var late = timeline.Next(10_500);

        Assert.Equal(RtpTimeline.ToUnits(1500), late);
    }
}
