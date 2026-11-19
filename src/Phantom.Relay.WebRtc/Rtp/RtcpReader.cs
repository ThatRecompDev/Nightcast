// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// Walks a (compound) RTCP packet from the sender. What a receiver needs of it is the sender report's NTP time, which
/// the next receiver report echoes so the sender can measure the round trip (RFC 3550 section 6.4.1).
/// </summary>
public static class RtcpReader
{
    private const int SsrcOffset = 4;
    private const int NtpOffset = 8;
    private const int RtpTimeOffset = 16;
    private const int LengthOffset = 2;

    /// <summary>The first sender report in <paramref name="compound"/>: its SSRC, NTP time and RTP time.</summary>
    [HotPath]
    public static bool TryReadSenderReport(ReadOnlySpan<byte> compound, out uint ssrc, out ulong ntpTimestamp, out uint rtpTimestamp)
    {
        ssrc = 0;
        ntpTimestamp = 0;
        rtpTimestamp = 0;
        int at = 0;

        while (at + RtcpFormat.HeaderLength <= compound.Length && compound[at] >> RtcpFormat.VersionShift == RtpFormat.Version)
        {
            int length = (BinaryPrimitives.ReadUInt16BigEndian(compound[(at + LengthOffset)..]) + 1) * RtcpFormat.WordLength;

            if (at + length > compound.Length)
            {
                return false;
            }

            if (compound[at + 1] == RtcpFormat.SenderReport && length >= RtcpFormat.ReceiverReportHeaderLength + RtcpFormat.SenderInfoLength)
            {
                ssrc = BinaryPrimitives.ReadUInt32BigEndian(compound[(at + SsrcOffset)..]);
                ntpTimestamp = BinaryPrimitives.ReadUInt64BigEndian(compound[(at + NtpOffset)..]);
                rtpTimestamp = BinaryPrimitives.ReadUInt32BigEndian(compound[(at + RtpTimeOffset)..]);

                return true;
            }

            at += length;
        }

        return false;
    }
}
