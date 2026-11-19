// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The retransmission timeout (RFC 9260 section 6.3.1): a smoothed round trip and its variation, measured from chunks
/// sent once, with RTO = SRTT + 4 RTTVAR, kept between a floor and a ceiling and doubled after each timeout. In the
/// clock's timestamp ticks.
/// </summary>
internal sealed class SctpRto
{
    /// <summary>RTO.Alpha, 1/8, as a shift.</summary>
    private const int AlphaShift = 3;

    /// <summary>RTO.Beta, 1/4, as a shift.</summary>
    private const int BetaShift = 2;

    private const int VarianceWeight = 4;
    private const int FirstVarianceShift = 1;
    private const int BackoffFactor = 2;

    private readonly long _initial;
    private readonly long _min;
    private readonly long _max;
    private long _smoothed;
    private long _variance;
    private bool _measured;

    public SctpRto(long initial, long min, long max)
    {
        _initial = initial;
        _min = min;
        _max = max;
        Current = initial;
    }

    public long Current { get; private set; }

    [HotPath]
    public void Measure(long roundTrip)
    {
        if (!_measured)
        {
            _smoothed = roundTrip;
            _variance = roundTrip >> FirstVarianceShift;
            _measured = true;
        }
        else
        {
            long delta = Math.Abs(_smoothed - roundTrip);
            _variance += (delta >> BetaShift) - (_variance >> BetaShift);
            _smoothed += (roundTrip >> AlphaShift) - (_smoothed >> AlphaShift);
        }

        Current = Math.Clamp(_smoothed + (VarianceWeight * _variance), _min, _max);
    }

    /// <summary>Doubles the timeout after a retransmission timer fired, up to the ceiling.</summary>
    [HotPath]
    public void Backoff()
    {
        Current = Math.Min(Current * BackoffFactor, _max);
    }

    /// <summary>Back to the initial timeout, for a new association.</summary>
    public void Reset()
    {
        _measured = false;
        Current = _initial;
    }
}
