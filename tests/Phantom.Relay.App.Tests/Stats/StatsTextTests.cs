// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.App.Stats;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Tests.Stats;

public sealed class StatsTextTests
{
    private static readonly StreamStats Streaming = StreamStats.Idle with
    {
        State = StreamState.Streaming,
        Width = 2560,
        Height = 1440,
        FramesReceivedPerSecond = 59.6,
        FramesDecodedPerSecond = 59.4,
        NewPicturesPerSecond = 29.7,
        RepeatedPicturesPerSecond = 29.7,
        Cadence = PictureCadence.Half,
        DecodeAverage = TimeSpan.FromMilliseconds(8.14),
        DecodeWorst = TimeSpan.FromMilliseconds(41.5),
        HandOffAverage = TimeSpan.FromMilliseconds(0.3),
        IceRoundTrip = TimeSpan.FromMilliseconds(0.4),
        DtlsTime = TimeSpan.FromMilliseconds(73),
        PacketsLost = 2,
        KeyframeRequests = 1,
        InputReportsSent = 40,
    };

    private static readonly PresentStats Present = new(TimeSpan.FromMilliseconds(6.2), TimeSpan.FromMilliseconds(9.5), TimeSpan.FromMilliseconds(0.1), OnGpu: true);

    [Fact]
    public void The_stats_read_the_same_on_a_dutch_machine()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

        try
        {
            string title = StatsText.WithStats(Streaming, Present, GpuPictureReport.Idle with { Path = GpuPictureReport.ZeroCopy });

            Assert.Equal("Phantom.Relay | 2560x1440 | 60 fps in, 59 decoded (30 new, 30 repeated, every other repeated) | decode 8.1 ms avg, 41.5 ms worst, hand-off 0.3 ms | to screen 6.2 ms avg, 9.5 ms worst, picture CPU 0.1 ms | GPU zero-copy | LSFG off | RTT 0.4 ms | DTLS 73.0 ms | lost 2, PLI 1, pad reports 40", title);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Without_stats_the_title_names_the_state_until_it_streams()
    {
        Assert.Equal("Phantom.Relay", StatsText.Plain(Streaming));
        Assert.Equal("Phantom.Relay: Connecting", StatsText.Plain(StreamStats.Idle with { State = StreamState.Connecting }));
        Assert.Equal("Phantom.Relay: failed: ICE found no path to the console", StatsText.Plain(StreamStats.Idle with { State = StreamState.Failed, FailureReason = "ICE found no path to the console" }));
    }

    [Fact]
    public void What_is_not_measured_yet_shows_a_dash()
    {
        string title = StatsText.WithStats(StreamStats.Idle with { State = StreamState.Connecting }, default, GpuPictureReport.Idle);

        Assert.Contains("RTT - | DTLS -", title, StringComparison.Ordinal);
    }
}
