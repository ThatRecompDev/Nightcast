// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream;

/// <summary>
/// Turns the stream's running totals into a snapshot's rates: frames received and decoded per second and the average
/// decode since the last reading, the totals as they are. The first reading has nothing to compare with, so its rates
/// are zero.
/// </summary>
public sealed class StreamStatsMeter
{
    private readonly TimeProvider _clock;

    private StreamCounters _last;
    private long _lastAt;
    private bool _started;

    public StreamStatsMeter(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary><paramref name="current"/> with the rates since the last reading and the totals of <paramref name="counters"/>.</summary>
    public StreamStats Measure(StreamStats current, in StreamCounters counters)
    {
        long now = _clock.GetTimestamp();
        double seconds = _clock.GetElapsedTime(_lastAt, now).TotalSeconds;
        var next = current with
        {
            DecodeWorst = counters.DecodeWorst,
            PacketsLost = counters.PacketsLost,
            KeyframeRequests = counters.KeyframeRequests,
            InputReportsSent = counters.InputReportsSent,
            Width = counters.Width,
            Height = counters.Height,
        };

        if (_started && seconds > 0)
        {
            next = next with
            {
                FramesReceivedPerSecond = (counters.FramesReceived - _last.FramesReceived) / seconds,
                FramesDecodedPerSecond = (counters.FramesDecoded - _last.FramesDecoded) / seconds,
                DecodeAverage = Average(counters),
            };
        }

        _started = true;
        _last = counters;
        _lastAt = now;

        return next;
    }

    private TimeSpan Average(in StreamCounters counters)
    {
        long units = counters.UnitsDecoded - _last.UnitsDecoded;

        if (units <= 0)
        {
            return TimeSpan.Zero;
        }

        return (counters.DecodeTotal - _last.DecodeTotal) / units;
    }
}
