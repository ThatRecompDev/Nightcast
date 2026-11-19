// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// One direction of an SRTP session (RFC 3711): one transform and the per-SSRC state of up to a fixed number of
/// sources, all made up front. The peer's direction unprotects what arrives (index estimation, replay check, tag,
/// decryption, then the state moves on); this side's direction protects what is sent, SRTCP feedback above all, with
/// its own SRTCP index. Packets are changed in place; nothing is allocated per packet.
/// </summary>
public sealed class SrtpContext
{
    /// <summary>
    /// How far behind the highest index a packet may still arrive. RFC 3711 asks at least 64; a video stream at 30
    /// Mbit/s sends some 3000 packets a second, and a retransmitted packet answers a NACK a round trip later.
    /// </summary>
    public const int ReplayWindowPackets = 1024;

    private readonly SrtpTransform _transform;
    private readonly SrtpSourceState[] _sources;
    private uint _rtcpIndex;

    public SrtpContext(SrtpTransform transform, int maxSources)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSources, 1);

        _transform = transform;
        _sources = new SrtpSourceState[maxSources];

        for (int i = 0; i < maxSources; i++)
        {
            _sources[i] = new SrtpSourceState(ReplayWindowPackets);
        }
    }

    /// <summary>The octets protecting adds to an RTP packet.</summary>
    public int RtpOverhead => _transform.RtpOverhead;

    /// <summary>The octets protecting adds to an RTCP packet.</summary>
    public int RtcpOverhead => _transform.RtcpOverhead;

    /// <summary>The context of one direction, from its master key under <paramref name="profile"/>.</summary>
    public static SrtpContext Create(SrtpProfile profile, SrtpMasterKey masterKey, int maxSources)
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        var keys = SrtpSessionKeys.Derive(profile, masterKey.Key, masterKey.Salt);

        return new SrtpContext(SrtpTransform.Create(profile, keys), maxSources);
    }

    /// <summary>The state of <paramref name="ssrc"/>'s stream, or null before its first packet.</summary>
    public SrtpSourceState? SourceOf(uint ssrc)
    {
        foreach (var stream in _sources)
        {
            if (stream.InUse && stream.Ssrc == ssrc)
            {
                return stream;
            }
        }

        return null;
    }

    /// <summary>
    /// Unprotects a received SRTP packet in place; <paramref name="length"/> is the RTP packet's. False for a packet
    /// that is malformed, replayed or too old, whose tag does not match, or of an SSRC beyond the sources' number.
    /// </summary>
    [HotPath]
    public bool TryUnprotectRtp(Span<byte> packet, out int length)
    {
        length = 0;

        if (!RtpFormat.TryGetHeaderLength(packet, out int headerLength))
        {
            return false;
        }

        uint ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet[RtpFormat.SsrcOffset..]);
        var stream = SourceOrFree(ssrc);

        if (stream is null)
        {
            return false;
        }

        long index = stream.EstimateIndex(BinaryPrimitives.ReadUInt16BigEndian(packet[RtpFormat.SequenceOffset..]));

        if (!stream.Rtp.IsFresh(index) || !_transform.TryUnprotectRtp(packet, headerLength, RolloverOf(index), out length))
        {
            return false;
        }

        stream.Claim(ssrc);
        stream.Rtp.Accept(index);

        return true;
    }

    /// <summary>Protects an RTP packet in <paramref name="buffer"/>[..<paramref name="length"/>], which needs room for the tag.</summary>
    [HotPath]
    public bool TryProtectRtp(Span<byte> buffer, int length, out int protectedLength)
    {
        protectedLength = 0;

        if (!RtpFormat.TryGetHeaderLength(buffer[..length], out int headerLength))
        {
            return false;
        }

        uint ssrc = BinaryPrimitives.ReadUInt32BigEndian(buffer[RtpFormat.SsrcOffset..]);
        var stream = SourceOrFree(ssrc);

        if (stream is null)
        {
            return false;
        }

        long index = stream.EstimateIndex(BinaryPrimitives.ReadUInt16BigEndian(buffer[RtpFormat.SequenceOffset..]));

        if (index < 0 || !_transform.TryProtectRtp(buffer, length, headerLength, RolloverOf(index), out protectedLength))
        {
            return false;
        }

        stream.Claim(ssrc);
        stream.Rtp.Accept(index);

        return true;
    }

    /// <summary>Unprotects a received SRTCP packet in place; false when malformed, replayed, too old or forged.</summary>
    [HotPath]
    public bool TryUnprotectRtcp(Span<byte> packet, out int length)
    {
        length = 0;

        if (packet.Length < SrtpFormat.RtcpHeaderLength + _transform.RtcpOverhead)
        {
            return false;
        }

        uint ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet[SrtpFormat.RtcpSsrcOffset..]);
        var stream = SourceOrFree(ssrc);

        if (stream is null || !stream.Rtcp.IsFresh(PeekRtcpIndex(packet)))
        {
            return false;
        }

        if (!_transform.TryUnprotectRtcp(packet, out length, out uint index))
        {
            return false;
        }

        stream.Claim(ssrc);
        stream.Rtcp.Accept(index);

        return true;
    }

    /// <summary>
    /// Protects an RTCP packet (or compound) in <paramref name="buffer"/>[..<paramref name="length"/>] with the next
    /// SRTCP index; the buffer needs <see cref="RtcpOverhead"/> octets of room after it.
    /// </summary>
    [HotPath]
    public bool TryProtectRtcp(Span<byte> buffer, int length, out int protectedLength)
    {
        if (!_transform.TryProtectRtcp(buffer, length, _rtcpIndex, out protectedLength))
        {
            return false;
        }

        _rtcpIndex = (_rtcpIndex + 1) & SrtpFormat.RtcpIndexMask;

        return true;
    }

    private static uint RolloverOf(long index)
    {
        return (uint)(index >> SrtpFormat.SequenceBits);
    }

    /// <summary>The SRTCP index in the clear, read before the tag is checked so a replay costs no decryption.</summary>
    private long PeekRtcpIndex(ReadOnlySpan<byte> packet)
    {
        int at = packet.Length - _transform.RtcpIndexFromEnd;

        return BinaryPrimitives.ReadUInt32BigEndian(packet[at..]) & SrtpFormat.RtcpIndexMask;
    }

    /// <summary>The stream of <paramref name="ssrc"/>, else a free one (claimed only once a packet verifies), else null.</summary>
    private SrtpSourceState? SourceOrFree(uint ssrc)
    {
        SrtpSourceState? free = null;

        foreach (var stream in _sources)
        {
            if (stream.InUse && stream.Ssrc == ssrc)
            {
                return stream;
            }

            if (!stream.InUse && free is null)
            {
                free = stream;
            }
        }

        return free;
    }
}
