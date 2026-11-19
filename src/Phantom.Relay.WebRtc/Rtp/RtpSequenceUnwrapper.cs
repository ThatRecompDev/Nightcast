// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// Extends 16-bit RTP sequence numbers to 64 bits: each number is placed within half the range (32768) of the highest
/// one seen, so the count goes on across wraps and a late packet from before a wrap lands before it. The first number
/// is taken as it is; a packet from before it comes out negative.
/// </summary>
public sealed class RtpSequenceUnwrapper
{
    private long _highest;
    private bool _started;

    /// <summary>The highest extended sequence number so far.</summary>
    public long Highest => _highest;

    [HotPath]
    public long Unwrap(ushort sequence)
    {
        if (!_started)
        {
            _started = true;
            _highest = sequence;

            return sequence;
        }

        // The signed 16-bit distance from the highest: forward up to 32767, backward up to 32768.
        long extended = _highest + (short)(sequence - (ushort)_highest);

        if (extended > _highest)
        {
            _highest = extended;
        }

        return extended;
    }

    public void Reset()
    {
        _highest = 0;
        _started = false;
    }
}
