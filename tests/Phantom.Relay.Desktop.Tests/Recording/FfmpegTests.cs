// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class FfmpegTests
{
    private static readonly string Profile = Path.Combine("users", "player");
    private static readonly string Scoop = Path.Combine(Profile, "scoop", "apps", "ffmpeg", "current", "bin", "ffmpeg.exe");

    [Fact]
    public void The_first_ffmpeg_a_path_folder_holds_is_used()
    {
        var path = string.Join(Path.PathSeparator, "tools", "\"media\"", "bin");
        var present = new HashSet<string> { Path.Combine("media", "ffmpeg.exe"), Path.Combine("bin", "ffmpeg.exe"), Scoop };

        var found = FfmpegLocator.Find(path, Profile, present.Contains);

        Assert.Equal(Path.Combine("media", "ffmpeg.exe"), found);
    }

    [Fact]
    public void Without_one_on_the_path_scoops_is_used()
    {
        var found = FfmpegLocator.Find("tools", Profile, Scoop.Equals);

        Assert.Equal(Scoop, found);
    }

    [Fact]
    public void Without_either_there_is_none()
    {
        Assert.Null(FfmpegLocator.Find(null, Profile, _ => false));
        Assert.Null(FfmpegLocator.Find(string.Empty, string.Empty, _ => true));
    }

    [Fact]
    public void A_progress_line_gives_the_output_time()
    {
        Assert.True(FfmpegProgress.TryRead("out_time_us=1500000", out var time));
        Assert.Equal(TimeSpan.FromSeconds(1.5), time);
    }

    [Fact]
    public void Other_lines_and_a_time_not_known_yet_give_none()
    {
        Assert.False(FfmpegProgress.TryRead("frame=10", out _));
        Assert.False(FfmpegProgress.TryRead("out_time_us=N/A", out _));
        Assert.False(FfmpegProgress.TryRead(null, out _));
    }

    [Fact]
    public void The_percent_is_of_the_whole_and_kept_within_it()
    {
        Assert.Equal(25, FfmpegProgress.Percent(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(40)));
        Assert.Equal(100, FfmpegProgress.Percent(TimeSpan.FromSeconds(41), TimeSpan.FromSeconds(40)));
        Assert.Equal(0, FfmpegProgress.Percent(TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }
}
