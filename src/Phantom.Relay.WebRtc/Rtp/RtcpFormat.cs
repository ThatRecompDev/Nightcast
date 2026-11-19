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

    /// <summary>The PSFB format of application-layer feedback (RFC 4585 section 6.4), the format REMB uses.</summary>
    public const int ApplicationLayerFormat = 15;

    /// <summary>The RTPFB format of transport-wide feedback (draft-holmer-rmcat-transport-wide-cc-extensions-01).</summary>
    public const int TransportWideFormat = 15;

    /// <summary>REMB's unique identifier after the feedback header: the ASCII letters 'R' 'E' 'M' 'B'.</summary>
    public const uint RembIdentifier = 0x52454D42;

    /// <summary>A REMB's fixed part: the feedback header, the identifier and the count, exponent and mantissa word.</summary>
    public const int RembHeaderLength = 20;

    /// <summary>The count of SSRCs a REMB applies to is one octet.</summary>
    public const int MaxRembSsrcs = 255;

    /// <summary>The bitrate word: the SSRC count in the top 8 bits, a 6-bit exponent, an 18-bit mantissa.</summary>
    public const int RembCountShift = 24;

    public const int RembExponentShift = 18;

    public const ulong RembMaxMantissa = (1 << RembExponentShift) - 1;

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

    /// <summary>The length of a REMB naming <paramref name="ssrcs"/> media sources.</summary>
    public static int RembLength(int ssrcs)
    {
        return RembHeaderLength + (ssrcs * SsrcLength);
    }

    /// <summary>The length of a receiver report with <paramref name="blocks"/> report blocks.</summary>
    public static int ReceiverReportLength(int blocks)
    {
        return ReceiverReportHeaderLength + (blocks * ReportBlockLength);
    }
}
