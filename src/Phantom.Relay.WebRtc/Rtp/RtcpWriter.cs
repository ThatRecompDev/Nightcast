// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// Writes the RTCP a receiver sends into a caller's buffer: the generic NACK (RFC 4585 section 6.2.1) for lost
/// packets, the picture loss indication (section 6.3.1) for a keyframe, the receiver estimated maximum bitrate (REMB)
/// and the receiver report (RFC 3550 section 6.4.2). Each returns its length; WebRTC's reduced-size RTCP (RFC 5506) lets each go out alone or be appended to the
/// others to make a compound packet. Allocation-free.
/// </summary>
public static class RtcpWriter
{
    private const int CountOffset = 0;
    private const int TypeOffset = 1;
    private const int LengthOffset = 2;
    private const int SenderOffset = 4;
    private const int MediaOffset = 8;

    /// <summary>A PLI from <paramref name="senderSsrc"/> asking <paramref name="mediaSsrc"/> for a keyframe.</summary>
    [HotPath]
    public static int WritePictureLossIndication(Span<byte> destination, uint senderSsrc, uint mediaSsrc)
    {
        WriteHeader(destination, RtcpFormat.PictureLossFormat, RtcpFormat.PayloadFeedback, RtcpFormat.PictureLossLength);
        BinaryPrimitives.WriteUInt32BigEndian(destination[SenderOffset..], senderSsrc);
        BinaryPrimitives.WriteUInt32BigEndian(destination[MediaOffset..], mediaSsrc);

        return RtcpFormat.PictureLossLength;
    }

    /// <summary>
    /// A generic NACK of the <paramref name="lost"/> sequence numbers, in sending order: each entry names a packet and
    /// flags up to 16 more after it. Duplicates are skipped. Returns 0 when nothing is lost.
    /// </summary>
    [HotPath]
    public static int WriteGenericNack(Span<byte> destination, uint senderSsrc, uint mediaSsrc, ReadOnlySpan<ushort> lost)
    {
        if (lost.IsEmpty)
        {
            return 0;
        }

        if (destination.Length < RtcpFormat.MaxNackLength(lost.Length))
        {
            throw new ArgumentException("The buffer has no room for the NACK.", nameof(destination));
        }

        int at = RtcpFormat.FeedbackHeaderLength;
        ushort packetId = lost[0];
        int mask = 0;

        for (int i = 1; i < lost.Length; i++)
        {
            int distance = (ushort)(lost[i] - packetId);

            if (distance == 0)
            {
                continue;
            }

            if (distance <= RtcpFormat.NackMaskPackets)
            {
                mask |= 1 << (distance - 1);
                continue;
            }

            at = WriteNackEntry(destination, at, packetId, mask);
            packetId = lost[i];
            mask = 0;
        }

        at = WriteNackEntry(destination, at, packetId, mask);
        WriteHeader(destination, RtcpFormat.GenericNackFormat, RtcpFormat.TransportFeedback, at);
        BinaryPrimitives.WriteUInt32BigEndian(destination[SenderOffset..], senderSsrc);
        BinaryPrimitives.WriteUInt32BigEndian(destination[MediaOffset..], mediaSsrc);

        return at;
    }

    /// <summary>
    /// A receiver estimated maximum bitrate (REMB, draft-alvestrand-rmcat-remb-03 section 2.2): application-layer
    /// feedback (PSFB, FMT=15) from <paramref name="senderSsrc"/> with the media source 0, the identifier 'REMB', the
    /// count of <paramref name="ssrcs"/>, <paramref name="bitsPerSecond"/> as an 18-bit mantissa times two to a 6-bit
    /// exponent (the smallest exponent that fits, rounding down), then the SSRCs the bitrate applies to.
    /// </summary>
    [HotPath]
    public static int WriteReceiverEstimatedMaxBitrate(Span<byte> destination, uint senderSsrc, ulong bitsPerSecond, ReadOnlySpan<uint> ssrcs)
    {
        int length = RtcpFormat.RembLength(ssrcs.Length);

        if (ssrcs.IsEmpty || ssrcs.Length > RtcpFormat.MaxRembSsrcs || destination.Length < length)
        {
            throw new ArgumentException("A REMB names 1 to 255 sources and needs room for them.", nameof(ssrcs));
        }

        WriteHeader(destination, RtcpFormat.ApplicationLayerFormat, RtcpFormat.PayloadFeedback, length);
        BinaryPrimitives.WriteUInt32BigEndian(destination[SenderOffset..], senderSsrc);
        BinaryPrimitives.WriteUInt32BigEndian(destination[MediaOffset..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(destination[RtcpFormat.FeedbackHeaderLength..], RtcpFormat.RembIdentifier);
        BinaryPrimitives.WriteUInt32BigEndian(destination[(RtcpFormat.FeedbackHeaderLength + RtcpFormat.SsrcLength)..], RembBitrate(ssrcs.Length, bitsPerSecond));

        for (int i = 0; i < ssrcs.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination[(RtcpFormat.RembHeaderLength + (i * RtcpFormat.SsrcLength))..], ssrcs[i]);
        }

        return length;
    }

    /// <summary>A receiver report from <paramref name="senderSsrc"/> with one report block per source.</summary>
    [HotPath]
    public static int WriteReceiverReport(Span<byte> destination, uint senderSsrc, ReadOnlySpan<RtpReceiveStatistics> sources)
    {
        int length = RtcpFormat.ReceiverReportLength(sources.Length);

        if (sources.Length > RtcpFormat.MaxReportBlocks || destination.Length < length)
        {
            throw new ArgumentException("Too many sources, or no room for the report.", nameof(sources));
        }

        WriteHeader(destination, sources.Length, RtcpFormat.ReceiverReport, length);
        BinaryPrimitives.WriteUInt32BigEndian(destination[SenderOffset..], senderSsrc);

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].WriteReportBlock(destination.Slice(RtcpFormat.ReceiverReportHeaderLength + (i * RtcpFormat.ReportBlockLength), RtcpFormat.ReportBlockLength));
        }

        return length;
    }

    private static void WriteHeader(Span<byte> destination, int count, int type, int length)
    {
        destination[CountOffset] = (byte)(RtcpFormat.VersionBits | count);
        destination[TypeOffset] = (byte)type;
        BinaryPrimitives.WriteUInt16BigEndian(destination[LengthOffset..], (ushort)((length / RtcpFormat.WordLength) - 1));
    }

    /// <summary>REMB's bitrate word: the count, then the smallest exponent whose mantissa fits in 18 bits.</summary>
    private static uint RembBitrate(int count, ulong bitsPerSecond)
    {
        int exponent = 0;

        while ((bitsPerSecond >> exponent) > RtcpFormat.RembMaxMantissa)
        {
            exponent++;
        }

        return ((uint)count << RtcpFormat.RembCountShift) | ((uint)exponent << RtcpFormat.RembExponentShift) | (uint)(bitsPerSecond >> exponent);
    }

    private static int WriteNackEntry(Span<byte> destination, int at, ushort packetId, int mask)
    {
        BinaryPrimitives.WriteUInt16BigEndian(destination[at..], packetId);
        BinaryPrimitives.WriteUInt16BigEndian(destination[(at + sizeof(ushort))..], (ushort)mask);

        return at + RtcpFormat.NackEntryLength;
    }
}
