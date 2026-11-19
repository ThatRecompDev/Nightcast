// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests;

public sealed class StreamStatsMeterTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void The_first_reading_has_totals_and_no_rates()
    {
        var meter = new StreamStatsMeter(_clock);
        var counters = new StreamCounters(60, 58, 60, 0, 0, PictureCadence.Full, TimeSpan.FromMilliseconds(480), TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(20), 3, 1, 12, 2560, 1440, 0, 0);

        var stats = meter.Measure(StreamStats.Idle with { State = StreamState.Streaming }, counters);

        Assert.Equal(StreamState.Streaming, stats.State);
        Assert.Equal(0, stats.FramesReceivedPerSecond);

        Assert.Equal(3, stats.PacketsLost);
        Assert.Equal(1, stats.KeyframeRequests);
        Assert.Equal(12, stats.InputReportsSent);
        Assert.Equal((2560, 1440), (stats.Width, stats.Height));
        Assert.Equal(TimeSpan.FromMilliseconds(20), stats.DecodeWorst);
    }

    [Fact]
    public void Rates_and_the_average_decode_cover_the_time_since_the_last_reading()
    {
        var meter = new StreamStatsMeter(_clock);
        var first = meter.Measure(StreamStats.Idle, new StreamCounters(100, 90, 100, 50, 50, PictureCadence.Half, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(400), TimeSpan.Zero, 0, 0, 0, 0, 0, 1_000_000, 10_000));
        _clock.Advance(TimeSpan.FromSeconds(2));

        var second = meter.Measure(first, new StreamCounters(220, 208, 220, 110, 110, PictureCadence.Half, TimeSpan.FromMilliseconds(1760), TimeSpan.FromMilliseconds(436), TimeSpan.FromMilliseconds(41), 4, 2, 30, 2560, 1440, 4_500_000, 42_000));

        Assert.Equal(60, second.FramesReceivedPerSecond, 6);
        Assert.Equal(59, second.FramesDecodedPerSecond, 6);
        Assert.Equal(TimeSpan.FromMilliseconds(8), second.DecodeAverage);
        Assert.Equal(TimeSpan.FromMilliseconds(0.3), second.HandOffAverage);
        Assert.Equal((30d, 30d, PictureCadence.Half), (second.NewPicturesPerSecond, second.RepeatedPicturesPerSecond, second.Cadence));
        Assert.Equal(TimeSpan.FromMilliseconds(41), second.DecodeWorst);

        // 3 500 000 video octets and 32 000 sound octets in 2 s: 14 and 0.128 Mbit/s.
        Assert.Equal(14, second.VideoMegabitsPerSecond, 6);
        Assert.Equal(0.128, second.AudioMegabitsPerSecond, 6);
    }

    [Fact]
    public void A_snapshot_is_immutable_and_keeps_what_the_meter_does_not_measure()
    {
        var meter = new StreamStatsMeter(_clock);
        var before = StreamStats.Idle with { State = StreamState.Streaming, TitleId = "1234ABCD", DtlsTime = TimeSpan.FromMilliseconds(73) };

        var after = meter.Measure(before, new StreamCounters(1, 1, 1, 0, 0, PictureCadence.Full, TimeSpan.FromMilliseconds(9), TimeSpan.Zero, TimeSpan.FromMilliseconds(9), 0, 0, 0, 2560, 1440, 0, 0));

        Assert.NotSame(before, after);
        Assert.Equal(0, before.Width);
        Assert.Equal("1234ABCD", after.TitleId);
        Assert.Equal(TimeSpan.FromMilliseconds(73), after.DtlsTime);
        Assert.Equal(StreamState.Idle, StreamStats.Idle.State);
    }

    [Fact]
    public void No_decode_since_the_last_reading_averages_zero()
    {
        var meter = new StreamStatsMeter(_clock);
        var counters = new StreamCounters(10, 10, 10, 0, 0, PictureCadence.Full, TimeSpan.FromMilliseconds(80), TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0);
        var first = meter.Measure(StreamStats.Idle, counters);
        _clock.Advance(TimeSpan.FromSeconds(1));

        var second = meter.Measure(first, counters);

        Assert.Equal(TimeSpan.Zero, second.DecodeAverage);
        Assert.Equal(0, second.FramesReceivedPerSecond);
    }
}
