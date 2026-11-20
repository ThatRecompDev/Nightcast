// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.H264;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The video's RTP timestamps (90 kHz, 32 bits, wrapping every 13 hours) as Media Foundation times (100 ns units) from
/// the first: each unit's time counted from the first unit's timestamp as a whole, never added up step by step, so no
/// rounding gathers. A step is read signed, so a unit a little out of order is a little earlier, not 13 hours later.
/// </summary>
internal sealed class RtpTimeline
{
    /// <summary>Media Foundation's time units in a second.</summary>
    public const long UnitsPerSecond = 10_000_000;

    private long _ticks;
    private uint _last;
    private bool _started;

    /// <summary>The time of the unit stamped <paramref name="rtpTimestamp"/>, in 100 ns units since the first unit.</summary>
    public long Next(uint rtpTimestamp)
    {
        if (_started)
        {
            _ticks += unchecked((int)(rtpTimestamp - _last));
        }

        _last = rtpTimestamp;
        _started = true;

        return ToUnits(_ticks);
    }

    /// <summary>A span of the 90 kHz clock in 100 ns units, rounded down.</summary>
    public static long ToUnits(long ticks)
    {
        return ticks * UnitsPerSecond / H264Format.ClockRate;
    }
}
