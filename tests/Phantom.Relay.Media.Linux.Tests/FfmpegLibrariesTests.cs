// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Linux.Interop;

namespace Phantom.Relay.Media.Linux.Tests;

public sealed class FfmpegLibrariesTests
{
    // FFmpeg's AV_VERSION_INT(major, minor, micro).
    private static uint Packed(int major, int minor, int micro)
    {
        return (uint)((major << 16) | (minor << 8) | micro);
    }

    [Fact]
    public void TheMajorVersionIsTheTopBits()
    {
        Assert.Equal(62, FfmpegLibraries.Major(Packed(62, 28, 100)));
    }

    [Fact]
    public void FfmpegEightIsAccepted()
    {
        Assert.Null(FfmpegLibraries.CheckVersions(Packed(62, 28, 100), Packed(60, 26, 100)));
    }

    [Fact]
    public void AnotherMajorIsRefusedWithBothVersions()
    {
        var problem = FfmpegLibraries.CheckVersions(Packed(61, 19, 101), Packed(59, 39, 100));

        Assert.NotNull(problem);
        Assert.Contains("libavcodec 61", problem, StringComparison.Ordinal);
        Assert.Contains("libavutil 59", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingLibrariesAreSaidByName()
    {
        // The test machines are Windows ones, without FFmpeg's Linux libraries.
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        Assert.False(FfmpegLibraries.TryLoad(out var problem));
        Assert.Contains(FfmpegLibraries.AvUtil, problem, StringComparison.Ordinal);
    }
}
