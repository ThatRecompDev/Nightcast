// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// The arrival jitter of the last packets: each packet's transit (its arrival time less its RTP timestamp, both in
/// samples, from the first packet on) is kept for a window of packets, and the jitter is the spread between the
/// earliest and the latest. A constant network delay and the clocks' slow drift cancel out of it. Audio thread only.
/// </summary>
internal sealed class ArrivalJitter
{
    /// <summary>The packets it looks back over: 640 ms of 10 ms packets.</summary>
    public const int WindowPackets = 64;

    private const int Mask = WindowPackets - 1;

    private readonly double[] _transits = new double[WindowPackets];
    private readonly double _samplesPerTick;
    private bool _started;
    private long _firstArrival;
    private uint _lastTimestamp;
    private long _timeline;
    private int _count;
    private int _next;

    /// <param name="timestampFrequency">The injected clock's timestamp units a second.</param>
    public ArrivalJitter(long timestampFrequency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);

        _samplesPerTick = (double)OpusStreamFormat.SampleRate / timestampFrequency;
    }

    /// <summary>The spread of the window's transits, in samples at 48 kHz.</summary>
    public double SpreadSamples { get; private set; }

    [HotPath]
    public void Observe(long arrival, uint timestamp)
    {
        if (!_started)
        {
            _started = true;
            _firstArrival = arrival;
            _lastTimestamp = timestamp;
        }

        // The timestamp's distance from the last one, signed, unwraps it onto one timeline.
        _timeline += (int)(timestamp - _lastTimestamp);
        _lastTimestamp = timestamp;
        _transits[_next] = ((arrival - _firstArrival) * _samplesPerTick) - _timeline;
        _next = (_next + 1) & Mask;

        if (_count < WindowPackets)
        {
            _count++;
        }

        SpreadSamples = Spread();
    }

    /// <summary>Forgets the stream: its timestamps start over.</summary>
    public void Reset()
    {
        _started = false;
        _timeline = 0;
        _count = 0;
        _next = 0;
        SpreadSamples = 0;
    }

    [HotPath]
    private double Spread()
    {
        var earliest = double.MaxValue;
        var latest = double.MinValue;

        for (var index = 0; index < _count; index++)
        {
            earliest = Math.Min(earliest, _transits[index]);
            latest = Math.Max(latest, _transits[index]);
        }

        return latest - earliest;
    }
}
