// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// The cryptographic state SRTP keeps per SSRC: the rollover counter and highest sequence number, held as the highest
/// accepted 48-bit packet index, and the replay windows of SRTP and SRTCP. Made in advance by its context and claimed
/// by the first packet of an SSRC whose tag verifies.
/// </summary>
public sealed class SrtpSourceState
{
    private const long SequenceMask = 0xFFFF;

    /// <summary>Half the sequence number space: the farthest a packet may be from the highest and still be placed.</summary>
    private const int HalfRange = 0x8000;

    public SrtpSourceState(int replayWindow)
    {
        Rtp = new SrtpReplayWindow(replayWindow);
        Rtcp = new SrtpReplayWindow(replayWindow);
    }

    public uint Ssrc { get; private set; }

    public bool InUse { get; private set; }

    public SrtpReplayWindow Rtp { get; }

    public SrtpReplayWindow Rtcp { get; }

    /// <summary>The rollover counter: how often the sequence number has wrapped (0 before the first packet).</summary>
    public uint RolloverCounter => (uint)(Math.Max(Rtp.Highest, 0) >> SrtpFormat.SequenceBits);

    public void Claim(uint ssrc)
    {
        Ssrc = ssrc;
        InUse = true;
    }

    /// <summary>
    /// The packet index of <paramref name="sequence"/> (RFC 3711 section 3.3.1): the rollover counter that places it
    /// nearest the highest sequence number so far, so a wrap counts on and a late packet from before a wrap goes back.
    /// -1 for a packet that would come before the stream's first.
    /// </summary>
    [HotPath]
    public long EstimateIndex(ushort sequence)
    {
        long highest = Rtp.Highest;

        if (highest < 0)
        {
            return sequence;
        }

        long rollover = highest >> SrtpFormat.SequenceBits;
        long last = highest & SequenceMask;
        long guess = rollover;

        if (last < HalfRange && sequence - last > HalfRange)
        {
            guess = rollover - 1;
        }
        else if (last >= HalfRange && last - HalfRange > sequence)
        {
            guess = rollover + 1;
        }

        if (guess < 0)
        {
            return -1;
        }

        return (guess << SrtpFormat.SequenceBits) | sequence;
    }
}
