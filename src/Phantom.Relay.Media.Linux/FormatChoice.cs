// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Media.Linux.Interop;

namespace Phantom.Relay.Media.Linux;

/// <summary>
/// The picture format the decoder asks libavcodec for, out of the ones it offers for a stream (its get_format
/// callback): VA-API surfaces when the hwaccel can take the stream, else 4:2:0 planes in software (libavcodec's own
/// H.264 decoder, on the CPU), else none, which fails the decode.
/// </summary>
internal static class FormatChoice
{
    [HotPath]
    public static int Choose(ReadOnlySpan<int> offered)
    {
        if (offered.Contains(FfmpegLayout.PixelFormatVaapi))
        {
            return FfmpegLayout.PixelFormatVaapi;
        }

        foreach (var format in offered)
        {
            if (IsPlanar420(format))
            {
                return format;
            }
        }

        return FfmpegLayout.PixelFormatNone;
    }

    /// <summary>Whether a format is 4:2:0 in three 8-bit planes (studio or full range).</summary>
    public static bool IsPlanar420(int format)
    {
        return format == FfmpegLayout.PixelFormatYuv420P || format == FfmpegLayout.PixelFormatYuvj420P;
    }
}
