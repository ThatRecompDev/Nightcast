// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Recording;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Media.Windows.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class RecordingReportTests
{
    private static readonly RecordingSummary Summary = new(
        new DateTimeOffset(2026, 11, 20, 21, 4, 3, TimeSpan.Zero),
        new RecordingSettings("LSFG x2 (quality, flow 1.00) 0.8 ms", 2, "FSR 1 to 3840x2160"),
        2,
        60,
        "console 30 fps",
        "Relay LSFG x2",
        new SourceOutcome(new Mp4Facts(2541, 2, 2560, 1440, TimeSpan.FromSeconds(42.35), "H.264", 165_000_000), null, 2541, 3, 0, 0),
        new ShownOutcome(null, "no picture was shown on the GPU path while it recorded", "no encoder", 0, TimeSpan.Zero, 0, 0, 0, 0, 0));

    [Fact]
    public void The_report_tells_the_settings_and_each_files_counts()
    {
        var text = RecordingReport.Text(Summary, "not made: both files are needed");

        Assert.Contains("frame generation: LSFG x2 (quality, flow 1.00) 0.8 ms (shown at x2, 60 fps)", text, StringComparison.Ordinal);
        Assert.Contains("upscaler: FSR 1 to 3840x2160", text, StringComparison.Ordinal);
        Assert.Contains("2541 frames (2 key), 2560x1440, 42.350 s, H.264, 31.2 Mbit/s", text, StringComparison.Ordinal);
        Assert.Contains("skipped before the first key frame 3", text, StringComparison.Ordinal);
        Assert.Contains("not made: no picture was shown on the GPU path while it recorded", text, StringComparison.Ordinal);
        Assert.Contains("compare.mp4, compare-half-speed.mp4: not made: both files are needed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_names_no_path()
    {
        var text = RecordingReport.Text(Summary, "made by ffmpeg after the recording");

        Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Users", text, StringComparison.Ordinal);
    }
}
