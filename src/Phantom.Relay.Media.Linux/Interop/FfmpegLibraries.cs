// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Linux.Interop;

/// <summary>
/// The FFmpeg libraries the decoder loads, by soname, and the major versions their mirrored layout
/// (<see cref="FfmpegLayout"/>) is for: FFmpeg 8.1's libavcodec 62 and libavutil 60. A struct's layout only changes
/// with a library's major version, so a library of the same major is safe to use and any other is refused.
/// </summary>
internal static class FfmpegLibraries
{
    /// <summary>libavcodec of FFmpeg 8.x.</summary>
    public const string AvCodec = "libavcodec.so.62";

    /// <summary>libavutil of FFmpeg 8.x.</summary>
    public const string AvUtil = "libavutil.so.60";

    /// <summary>libva itself (the system's; FFmpeg's libavutil links it), for the driver's name.</summary>
    public const string Va = "libva.so.2";

    public const int AvCodecMajor = 62;
    public const int AvUtilMajor = 60;

    // FFmpeg's AV_VERSION_INT: major << 16 | minor << 8 | micro.
    private const int MajorShift = 16;

    /// <summary>The major version in one of FFmpeg's packed version numbers (avcodec_version, avutil_version).</summary>
    public static int Major(uint packed)
    {
        return (int)(packed >> MajorShift);
    }

    /// <summary>
    /// Loads libavutil, then libavcodec (which needs it), from the app's folder or the system's; false and why when
    /// either is missing. Loading libavutil first lets libavcodec find it beside the app even without its $ORIGIN path.
    /// </summary>
    public static bool TryLoad(out string? problem)
    {
        var assembly = typeof(FfmpegLibraries).Assembly;

        foreach (var name in (ReadOnlySpan<string>)[AvUtil, AvCodec])
        {
            if (!NativeLibrary.TryLoad(name, assembly, null, out _))
            {
                problem = $"{name} is not there (FFmpeg 8 with VA-API; scripts/build/build-ffmpeg-linux.ps1 builds it)";

                return false;
            }
        }

        problem = null;

        return true;
    }

    /// <summary>Why the loaded libraries cannot be used: another major version than the layout's; null when they can.</summary>
    public static string? CheckVersions(uint avCodec, uint avUtil)
    {
        if (Major(avCodec) == AvCodecMajor && Major(avUtil) == AvUtilMajor)
        {
            return null;
        }

        return $"FFmpeg's libavcodec {Major(avCodec)} and libavutil {Major(avUtil)} were found; the decoder is built for {AvCodecMajor} and {AvUtilMajor} (FFmpeg 8)";
    }
}
