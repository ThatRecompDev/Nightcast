// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Lsfg.Pacing;

namespace Phantom.Relay.Desktop.Tests.Pictures;

public sealed class DisplayRateTests
{
    private static readonly TimeSpan Source = TimeSpan.FromSeconds(1.0 / 30);

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Before_a_display_is_measured_nothing_is_capped_and_a_30_fps_game_is_assumed()
    {
        var rate = new DisplayRate();

        Assert.Equal(4, rate.Limit(4));
        Assert.Equal(120, rate.FramesPerSecond(4), 1);
        Assert.Equal(0, rate.Hertz);
    }

    [Theory]
    [InlineData(60, 4, 2)]
    [InlineData(59.94, 4, 2)]
    [InlineData(100, 4, 3)]
    [InlineData(120, 4, 4)]
    [InlineData(144, 4, 4)]
    [InlineData(240, 4, 4)]
    [InlineData(240, 3, 3)]
    public void A_30_fps_game_is_shown_at_no_more_frames_than_the_display_refreshes(double hertz, int requested, int allowed)
    {
        var rate = new DisplayRate();
        var pacer = new FramePacer(_clock, requested, Source, TimeSpan.FromSeconds(1.0 / 60));
        var refresh = TimeSpan.FromSeconds(1 / hertz);

        Assert.False(rate.Observe(pacer));

        while (!pacer.RefreshSettled)
        {
            _ = pacer.OnRefresh();
            _clock.Advance(refresh);
        }

        Assert.True(rate.Observe(pacer));
        Assert.Equal(allowed, rate.Limit(requested));
        Assert.Equal(hertz, rate.Hertz, 0);
    }
}
