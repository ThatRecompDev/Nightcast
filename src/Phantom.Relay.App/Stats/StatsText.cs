// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Setup;

namespace Phantom.Relay.App.Stats;

/// <summary>
/// The window's title for a stats snapshot: the app's name and where the stream is, and with the stats shown the
/// picture's size, frames received and decoded per second, the decode time (average and worst), the path's round
/// trip, the DTLS time, losses, keyframe requests and pad reports. Numbers in the invariant culture.
/// </summary>
public static class StatsText
{
    private const string NotMeasured = "-";

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
    public static string WithStats(StreamStats stats)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Plain(stats)} | {stats.Width}x{stats.Height} | {stats.FramesReceivedPerSecond:F0} fps in, {stats.FramesDecodedPerSecond:F0} decoded | decode {Milliseconds(stats.DecodeAverage)} avg, {Milliseconds(stats.DecodeWorst)} worst | RTT {Milliseconds(stats.IceRoundTrip)} | DTLS {Milliseconds(stats.DtlsTime)} | lost {stats.PacketsLost}, PLI {stats.KeyframeRequests}, pad reports {stats.InputReportsSent}");
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
