// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// Holds the playout delay at its target by resampling a hair faster or slower, at most 0.5 %: the console's clock
/// and the sound card's never quite agree, and the same small ratio absorbs a target that moved. A proportional term
/// follows the delay (a full 0.5 % at 10 ms off, so it settles in about two seconds) and an integral term learns the
/// clocks' steady drift, so the delay ends on its target rather than beside it. The delay it sees is each packet's
/// headroom on arrival, smoothed over half a second. Audio thread only.
/// </summary>
internal sealed class DriftController
{
    /// <summary>The most the ratio moves from 1: 0.5 %, a twelfth of a semitone, unheard on game sound.</summary>
    public const double MaxAdjustment = 0.005;

    /// <summary>The delay error that asks for the whole adjustment.</summary>
    private const double FullAdjustmentMilliseconds = 10;

    /// <summary>The integral's time: four times the proportional loop's, so it settles without ringing.</summary>
    private const double IntegralSeconds = 8;

    private const double SmoothingMilliseconds = 500;

    /// <summary>How long the target must hold still before the error counts as drift again.</summary>
    private const int TargetSettleMilliseconds = 1000;

    private readonly double _proportional;
    private readonly double _smoothing;
    private readonly int _settle;
    private double _integral;
    private double _lastTarget;
    private int _sinceTargetMoved;
    private bool _primed;

    public DriftController()
    {
        _proportional = MaxAdjustment / (FullAdjustmentMilliseconds * OpusStreamFormat.SamplesPerMillisecond);
        _smoothing = SmoothingMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _settle = TargetSettleMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        Ratio = 1d;
    }

    /// <summary>Input samples consumed per sample of the device's clock: above 1 plays faster.</summary>
    public double Ratio { get; private set; }

    /// <summary>The smoothed delay, in samples at 48 kHz.</summary>
    public double SmoothedDelay { get; private set; }

    /// <summary>A packet arrived <paramref name="headroomSamples"/> before its first sample is due.</summary>
    [HotPath]
    public void Observe(double headroomSamples, int packetSamples)
    {
        if (!_primed)
        {
            _primed = true;
            SmoothedDelay = headroomSamples;

            return;
        }

        var weight = packetSamples / (packetSamples + _smoothing);
        SmoothedDelay += (headroomSamples - SmoothedDelay) * weight;
    }

    /// <summary>After <paramref name="elapsedSamples"/> more were played, steers toward <paramref name="targetSamples"/>.</summary>
    [HotPath]
    public void Update(double targetSamples, int elapsedSamples)
    {
        if (!_primed)
        {
            return;
        }

        var proportional = (SmoothedDelay - targetSamples) * _proportional;
        TrackTarget(targetSamples, elapsedSamples);

        // The drift is learnt only while the target holds and the delay is near: else the error is the target moving.
        if (_sinceTargetMoved >= _settle && Math.Abs(proportional) < MaxAdjustment)
        {
            var seconds = elapsedSamples / (double)OpusStreamFormat.SampleRate;
            _integral = Math.Clamp(_integral + (proportional * seconds / IntegralSeconds), -MaxAdjustment, MaxAdjustment);
        }

        Ratio = 1d + Math.Clamp(_integral + proportional, -MaxAdjustment, MaxAdjustment);
    }

    /// <summary>The delay starts over (a new start, a cut backlog); the drift learnt so far stays, as the clocks did.</summary>
    [HotPath]
    public void Restart()
    {
        _primed = false;
        Ratio = 1d + _integral;
    }

    [HotPath]
    private void TrackTarget(double targetSamples, int elapsedSamples)
    {
        if (targetSamples != _lastTarget)
        {
            _lastTarget = targetSamples;
            _sinceTargetMoved = 0;

            return;
        }

        _sinceTargetMoved = Math.Min(_settle, _sinceTargetMoved + elapsedSamples);
    }
}
