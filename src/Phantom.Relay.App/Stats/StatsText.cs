// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;
using Phantom.Relay.Stream.Setup;

namespace Phantom.Relay.App.Stats;

/// <summary>
/// The window's title for a stats snapshot: the app's name and where the stream is, and with the stats shown the
/// picture's size, frames received and decoded per second (the new pictures, the repeats and the cadence), the decode time (average and worst) and of it the CPU's
/// hand-off of the picture to the renderer, the time from decode to present (average and worst) and the render
/// thread's CPU time for the picture, how the pictures come (on the GPU or read back) and what frame generation does,
/// the path's round trip, the DTLS time, losses, keyframe requests and pad reports. Numbers in the invariant culture.
/// </summary>
public static class StatsText
{
    private const string NotMeasured = "-";
    private const string FullRate = "every picture new";
    private const string HalfRate = "every other repeated";

    /// <summary>The longest title sokol shows: its 128-byte buffer less the terminator.</summary>
    public const int MaxTitleBytes = 127;

    /// <summary>The title without the stats: the name, then the state unless it streams, and why it failed.</summary>
    public static string Plain(StreamStats stats)
    {
        if (stats.State == StreamState.Streaming)
        {
            return RelayApp.Name;
        }

        if (stats.State == StreamState.Failed)
        {
            return $"{RelayApp.Name}: failed: {stats.FailureReason}";
        }

        return $"{RelayApp.Name}: {stats.State}";
    }

    /// <summary>
    /// The window's title with the stats, short enough for a title: sokol keeps 128 bytes of UTF-8 with the terminator
    /// and drops a longer title whole. The full line (<see cref="WithStats"/>) goes to the log.
    /// </summary>
    public static string Title(StreamStats stats, PresentStats present, GpuPictureReport pictures)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(pictures);

        string title = string.Create(
            CultureInfo.InvariantCulture,
            $"{Plain(stats)} | {stats.FramesReceivedPerSecond:F0} fps, {stats.NewPicturesPerSecond:F0} new | to screen {Milliseconds(present.DecodeToPresent)} | {PathShort(pictures.Path)} | {Before(pictures.FrameGeneration)} | RTT {Milliseconds(stats.IceRoundTrip)} | lost {stats.PacketsLost}");

        return Clamp(title);
    }

    /// <summary><paramref name="title"/> cut to at most <see cref="MaxTitleBytes"/> bytes of UTF-8, never in a pair.</summary>
    public static string Clamp(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        int length = title.Length;

        while (Encoding.UTF8.GetByteCount(title.AsSpan(0, length)) > MaxTitleBytes)
        {
            length--;

            if (length > 0 && char.IsHighSurrogate(title[length - 1]))
            {
                length--;
            }
        }

        return title[..length];
    }

    /// <summary>The full stats line, for the log.</summary>
    public static string WithStats(StreamStats stats, PresentStats present, GpuPictureReport pictures)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Plain(stats)} | {stats.Width}x{stats.Height} | {stats.FramesReceivedPerSecond:F0} fps in, {stats.FramesDecodedPerSecond:F0} decoded ({stats.NewPicturesPerSecond:F0} new, {stats.RepeatedPicturesPerSecond:F0} repeated, {Cadence(stats.Cadence)}) | decode {Milliseconds(stats.DecodeAverage)} avg, {Milliseconds(stats.DecodeWorst)} worst, hand-off {Milliseconds(stats.HandOffAverage)} | to screen {Milliseconds(present.DecodeToPresent)} avg, {Milliseconds(present.DecodeToPresentWorst)} worst, picture CPU {Milliseconds(present.PictureCpu)} | {pictures.Path} | {pictures.FrameGeneration} | RTT {Milliseconds(stats.IceRoundTrip)} | DTLS {Milliseconds(stats.DtlsTime)} | lost {stats.PacketsLost}, PLI {stats.KeyframeRequests}, pad reports {stats.InputReportsSent}");
    }

    /// <summary>The picture path without the read-back's reason, which the full line and the log carry.</summary>
    private static string PathShort(string path)
    {
        if (path.StartsWith(GpuPictureReport.ReadBack, StringComparison.Ordinal))
        {
            return GpuPictureReport.ReadBack;
        }

        return path;
    }

    /// <summary>The text before its first colon: "LSFG off" of "LSFG off: why".</summary>
    private static string Before(string text)
    {
        int colon = text.IndexOf(':', StringComparison.Ordinal);

        if (colon < 0)
        {
            return text;
        }

        return text[..colon];
    }

    private static string Cadence(PictureCadence cadence)
    {
        if (cadence == PictureCadence.Half)
        {
            return HalfRate;
        }

        return FullRate;
    }

    private static string Milliseconds(TimeSpan? span)
    {
        if (span is null)
        {
            return NotMeasured;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{span.Value.TotalMilliseconds:F1} ms");
    }
}
