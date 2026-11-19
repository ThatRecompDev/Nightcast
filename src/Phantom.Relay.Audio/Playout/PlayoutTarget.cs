// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// The playout delay the buffer aims at, latency first: it starts at a few packets, grows at once to cover the
/// measured arrival jitter and by a packet on every underrun, and shrinks slowly, a millisecond at a time, once the
/// arrivals have been steady for a while (longer after an underrun than after jitter). Audio thread only.
/// </summary>
internal sealed class PlayoutTarget
{
    /// <summary>Above the jitter's spread: a margin for the device thread's own timing.</summary>
    private const int SafetyMarginMilliseconds = 2;

    /// <summary>Steady time after growing on jitter before it shrinks.</summary>
    private const int JitterHoldMilliseconds = 2000;

    /// <summary>Steady time after an underrun before it shrinks: the device itself may have stalled.</summary>
    private const int UnderrunHoldMilliseconds = 10000;

    private const int ShrinkIntervalMilliseconds = 250;

    private const int ShrinkStepMilliseconds = 1;

    private readonly int _min;
    private readonly int _max;
    private readonly int _startPackets;
    private readonly int _margin;
    private readonly int _interval;
    private readonly int _step;
    private int _hold;
    private int _steady;
    private int _sinceShrink;

    public PlayoutTarget(PlayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MinDelayMilliseconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaxDelayMilliseconds, settings.MinDelayMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.StartPackets);

        _min = settings.MinDelayMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _max = settings.MaxDelayMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _startPackets = settings.StartPackets;
        _margin = SafetyMarginMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _interval = ShrinkIntervalMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _step = ShrinkStepMilliseconds * OpusStreamFormat.SamplesPerMillisecond;

        _hold = JitterHoldMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        Samples = _min;
    }

    /// <summary>The target delay, in samples at 48 kHz.</summary>
    public int Samples { get; private set; }

    /// <summary>The stream's first packet sets the start: a few packets of its duration.</summary>
    public void Initialize(int packetSamples)
    {
        Samples = Math.Clamp(_startPackets * packetSamples, _min, _max);
        Restart(JitterHoldMilliseconds);
    }

    /// <summary>The buffer ran dry: a packet more, at once, and a long hold before it shrinks again.</summary>
    [HotPath]
    public void Grow(int packetSamples)
    {
        Samples = Math.Min(_max, Samples + packetSamples);
        Restart(UnderrunHoldMilliseconds);
    }

    /// <summary>
    /// After <paramref name="elapsedSamples"/> more were played: up at once to cover <paramref name="spreadSamples"/>
    /// of jitter, or a step down when the hold has passed and the jitter allows it.
    /// </summary>
    [HotPath]
    public void Follow(double spreadSamples, int elapsedSamples)
    {
        var desired = Math.Clamp((int)Math.Ceiling(spreadSamples) + _margin, _min, _max);

        if (desired > Samples)
        {
            Samples = desired;
            Restart(JitterHoldMilliseconds);

            return;
        }

        if (_steady < _hold)
        {
            _steady += elapsedSamples;

            return;
        }

        _sinceShrink += elapsedSamples;

        if (_sinceShrink < _interval)
        {
            return;
        }

        _sinceShrink -= _interval;
        Samples = Math.Max(desired, Samples - _step);
    }

    [HotPath]
    private void Restart(int holdMilliseconds)
    {
        _hold = holdMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _steady = 0;
        _sinceShrink = 0;
    }
}
