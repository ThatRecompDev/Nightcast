// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
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

    /// <summary>The title with the stats.</summary>
    public static string WithStats(StreamStats stats, PresentStats present, GpuPictureReport pictures)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Plain(stats)} | {stats.Width}x{stats.Height} | {stats.FramesReceivedPerSecond:F0} fps in, {stats.FramesDecodedPerSecond:F0} decoded ({stats.NewPicturesPerSecond:F0} new, {stats.RepeatedPicturesPerSecond:F0} repeated, {Cadence(stats.Cadence)}) | decode {Milliseconds(stats.DecodeAverage)} avg, {Milliseconds(stats.DecodeWorst)} worst, hand-off {Milliseconds(stats.HandOffAverage)} | to screen {Milliseconds(present.DecodeToPresent)} avg, {Milliseconds(present.DecodeToPresentWorst)} worst, picture CPU {Milliseconds(present.PictureCpu)} | {pictures.Path} | {pictures.FrameGeneration} | RTT {Milliseconds(stats.IceRoundTrip)} | DTLS {Milliseconds(stats.DtlsTime)} | lost {stats.PacketsLost}, PLI {stats.KeyframeRequests}, pad reports {stats.InputReportsSent}");
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
