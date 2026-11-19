// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>The fixed values of RTCP (RFC 3550 section 6) and its feedback messages (RFC 4585).</summary>
public static class RtcpFormat
{
    /// <summary>The first octet's version bits (V=2); the low five bits are the count or the feedback format.</summary>
    public const byte VersionBits = 0x80;

    public const int VersionShift = 6;

    public const int CountMask = 0x1F;

    /// <summary>The common header: version and count, packet type, and the length in 32-bit words minus one.</summary>
    public const int HeaderLength = 4;

    public const int WordLength = 4;

    public const int SsrcLength = 4;

    public const int SenderReport = 200;

    public const int ReceiverReport = 201;

    /// <summary>Transport-layer feedback (RTPFB), the packet type of a generic NACK.</summary>
    public const int TransportFeedback = 205;

    /// <summary>Payload-specific feedback (PSFB), the packet type of a PLI.</summary>
    public const int PayloadFeedback = 206;

    /// <summary>The RTPFB format of a generic NACK.</summary>
    public const int GenericNackFormat = 1;

    /// <summary>The PSFB format of a picture loss indication.</summary>
    public const int PictureLossFormat = 1;

    /// <summary>A feedback message's common part: the header, the sender's SSRC and the media source's SSRC.</summary>
    public const int FeedbackHeaderLength = 12;

    /// <summary>A PLI is the common feedback part alone.</summary>
    public const int PictureLossLength = FeedbackHeaderLength;

    /// <summary>One NACK entry: a packet id and a bitmask of the 16 packets after it.</summary>
    public const int NackEntryLength = 4;

    public const int NackMaskPackets = 16;

    /// <summary>A report block: SSRC, fraction and cumulative lost, extended highest sequence, jitter, LSR, DLSR.</summary>
    public const int ReportBlockLength = 24;

    /// <summary>The count field's five bits hold at most 31 report blocks.</summary>
    public const int MaxReportBlocks = 31;

    /// <summary>A receiver report's header and the reporter's SSRC.</summary>
    public const int ReceiverReportHeaderLength = 8;

    /// <summary>A sender report's sender info after the header and SSRC: NTP time, RTP time, packet and octet counts.</summary>
    public const int SenderInfoLength = 20;

    /// <summary>The length of a NACK of <paramref name="lostCount"/> packets at most (one entry each when none share).</summary>
    public static int MaxNackLength(int lostCount)
    {
        return FeedbackHeaderLength + (lostCount * NackEntryLength);
    }

    /// <summary>The length of a receiver report with <paramref name="blocks"/> report blocks.</summary>
    public static int ReceiverReportLength(int blocks)
    {
        return ReceiverReportHeaderLength + (blocks * ReportBlockLength);
    }
}
