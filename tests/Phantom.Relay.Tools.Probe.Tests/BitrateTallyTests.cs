// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Tools.Probe.Tests;

public sealed class BitrateTallyTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Each_second_gives_its_bitrate_and_the_seconds_with_video_make_the_average_and_peak()
    {
        var tally = new BitrateTally(_clock);

        Assert.Null(tally.Sample(Counts(0, 0, 0)));

        // A silent second before the console streams, then 1 750 000 and 2 000 000 octets: 14 and 16 Mbit/s.
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("    video 0.0 Mbit/s, 0 frames of 0 bytes on average; sound 0.00 Mbit/s", tally.Sample(Counts(0, 0, 0)));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("    video 14.0 Mbit/s, 60 frames of 29166 bytes on average; sound 0.13 Mbit/s", tally.Sample(Counts(1_750_000, 16_000, 60)));
        _clock.Advance(TimeSpan.FromSeconds(1));
        tally.Sample(Counts(3_750_000, 32_000, 120));

        Assert.Equal(15, tally.AverageVideo, 6);
        Assert.Equal(16, tally.PeakVideo, 6);
        Assert.Equal(0.128, tally.AverageAudio, 6);
    }

    private static MediaCounts Counts(long video, long audio, long units)
    {
        return new MediaCounts(0, 0, 0, 0, units, 0, 0, 0, 0) { VideoBytes = video, AudioBytes = audio };
    }
}
