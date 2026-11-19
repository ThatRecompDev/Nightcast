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
            Cadence = counters.Cadence,
        };

        if (_started && seconds > 0)
        {
            next = next with
            {
                FramesReceivedPerSecond = (counters.FramesReceived - _last.FramesReceived) / seconds,
                FramesDecodedPerSecond = (counters.FramesDecoded - _last.FramesDecoded) / seconds,
                NewPicturesPerSecond = (counters.NewPictures - _last.NewPictures) / seconds,
                RepeatedPicturesPerSecond = (counters.RepeatedPictures - _last.RepeatedPictures) / seconds,
                DecodeAverage = Average(counters.DecodeTotal - _last.DecodeTotal, counters),
                HandOffAverage = Average(counters.HandOffTotal - _last.HandOffTotal, counters),
            };
        }

        _started = true;
        _last = counters;
        _lastAt = now;

        return next;
    }

    // A time spent since the last reading, per unit decoded since.
    private TimeSpan Average(TimeSpan spent, in StreamCounters counters)
    {
        long units = counters.UnitsDecoded - _last.UnitsDecoded;

        if (units <= 0)
        {
            return TimeSpan.Zero;
        }

        return spent / units;
    }
}
