// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text;
using Phantom.Relay.App.Stats;
using Phantom.Relay.Profiles.Input;
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
        VideoMegabitsPerSecond = 14.04,
        AudioMegabitsPerSecond = 0.128,
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

            Assert.Equal("Nightcast | 2560x1440 | 60 fps in, 59 decoded (30 new, 30 repeated, every other repeated) | decode 8.1 ms avg, 41.5 ms worst, hand-off 0.3 ms | to screen 6.2 ms avg, 9.5 ms worst, picture CPU 0.1 ms | GPU zero-copy | frame generation off | video 14.0 Mbit/s, sound 0.13 Mbit/s | RTT 0.4 ms | DTLS 73.0 ms | lost 2, PLI 1, pad reports 40", title);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void The_upscaler_shows_by_name_in_the_title_and_whole_in_the_block_and_the_log()
    {
        var report = new GpuPictureReport(GpuPictureReport.ZeroCopy, "LSFG off");
        const string Upscale = "FSR 1 → 3840x2160 0.21 ms";

        string title = StatsText.Title(Streaming, Present, report, Upscale);
        string[] lines = StatsText.Lines(Streaming, Present, report, PadSource.Controller, Upscale);

        Assert.Contains(" | lost 2 | FSR 1", title, StringComparison.Ordinal);
        Assert.Equal(Upscale, lines[^2]);
        Assert.Contains($"LSFG off | {Upscale} | video", StatsText.WithStats(Streaming, Present, report, Upscale), StringComparison.Ordinal);
    }

    [Fact]
    public void The_titles_stats_fit_the_windows_title()
    {
        var report = new GpuPictureReport(GpuPictureReport.ZeroCopy, "LSFG x2 (quality) 0.8 ms");

        string title = StatsText.Title(Streaming, Present, report);

        Assert.Equal("Nightcast | 60 fps, 30 new | to screen 6.2 ms | GPU zero-copy | LSFG x2 (quality) 0.8 ms | RTT 0.4 ms | lost 2 | 14.0 Mbit/s", title);
        Assert.True(Encoding.UTF8.GetByteCount(title) <= StatsText.MaxTitleBytes);
    }

    [Fact]
    public void The_video_bitrate_ends_the_title_when_it_fits()
    {
        var report = new GpuPictureReport(GpuPictureReport.ZeroCopy, GpuPictureReport.GenerationOff);

        string title = StatsText.Title(Streaming, Present, report);

        Assert.EndsWith(" | lost 2 | 14.0 Mbit/s", title, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(title) <= StatsText.MaxTitleBytes);
    }

    [Fact]
    public void A_long_title_is_cut_to_what_the_window_keeps_and_its_reasons_left_to_the_log()
    {
        var failed = Streaming with { State = StreamState.Failed, FailureReason = new string('é', 200) };
        var report = new GpuPictureReport($"{GpuPictureReport.ReadBack}: the device opens no shared textures", "LSFG off: Lossless Scaling was not found");

        string title = StatsText.Title(failed, Present, report);

        Assert.True(Encoding.UTF8.GetByteCount(title) <= StatsText.MaxTitleBytes);
        Assert.Contains(" | to screen", StatsText.Title(Streaming, Present, report), StringComparison.Ordinal);
        Assert.DoesNotContain("not found", StatsText.Title(Streaming, Present, report), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_stats_the_title_names_the_state_until_it_streams()
    {
        Assert.Equal("Nightcast", StatsText.Plain(Streaming));
        Assert.Equal("Nightcast: Connecting", StatsText.Plain(StreamStats.Idle with { State = StreamState.Connecting }));
        Assert.Equal("Nightcast: failed: ICE found no path to the console", StatsText.Plain(StreamStats.Idle with { State = StreamState.Failed, FailureReason = "ICE found no path to the console" }));
    }

    [Fact]
    public void The_block_on_screen_reads_the_same_on_a_dutch_machine()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

        try
        {
            var report = new GpuPictureReport(GpuPictureReport.ZeroCopy, "LSFG x2 (quality) 0.8 ms");

            string[] lines = StatsText.Lines(Streaming, Present, report, PadSource.KeyboardMouse);

            Assert.Equal(
                [
                    "Streaming 2560x1440",
                    "60 fps in, 59 decoded: 30 new, 30 repeated (every other repeated)",
                    "decode 8.1 ms avg, 41.5 ms worst, hand-off 0.3 ms",
                    "to screen 6.2 ms avg, 9.5 ms worst, picture CPU 0.1 ms",
                    "GPU zero-copy",
                    "LSFG x2 (quality) 0.8 ms",
                    "RTT 0.4 ms, DTLS 73.0 ms, video 14.0 Mbit/s, sound 0.13",
                    "lost 2, PLI 1, pad reports 40, rumble 0",
                    "input: keyboard and mouse",
                ],
                lines);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void The_blocks_long_lines_are_cut_and_its_first_line_says_why_it_failed()
    {
        var failed = StreamStats.Idle with { State = StreamState.Failed, FailureReason = "ICE found no path to the console" };
        var report = new GpuPictureReport($"{GpuPictureReport.ReadBack}: {new string('x', 100)}", GpuPictureReport.GenerationOff);

        string[] lines = StatsText.Lines(failed, default, report, PadSource.Controller);

        Assert.Equal("Failed: ICE found no path to the console", lines[0]);
        Assert.Equal(StatsText.MaxLineCharacters, lines[4].Length);
        Assert.StartsWith(GpuPictureReport.ReadBack, lines[4], StringComparison.Ordinal);
        Assert.EndsWith("...", lines[4], StringComparison.Ordinal);
        Assert.Equal("input: controller", lines[^1]);
        Assert.All(lines, line => Assert.True(line.Length <= StatsText.MaxLineCharacters, line));
    }

    [Fact]
    public void Before_a_picture_the_blocks_first_line_is_the_state_alone()
    {
        string[] lines = StatsText.Lines(StreamStats.Idle with { State = StreamState.Connecting }, default, GpuPictureReport.Idle, PadSource.Controller);

        Assert.Equal("Connecting", lines[0]);
        Assert.Equal("RTT -, DTLS -, video 0.0 Mbit/s, sound 0.00", lines[6]);
    }

    [Fact]
    public void What_is_not_measured_yet_shows_a_dash()
    {
        string title = StatsText.WithStats(StreamStats.Idle with { State = StreamState.Connecting }, default, GpuPictureReport.Idle);

        Assert.Contains("RTT - | DTLS -", title, StringComparison.Ordinal);
    }
}
