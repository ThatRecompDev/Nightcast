// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

public sealed class RtcpReaderTests
{
    /// <summary>A PLI, then a sender report with no report blocks (NTP 0x0102030405060708, RTP time 0x0A0B0C0D).</summary>
    private static readonly byte[] Compound = Convert.FromHexString(
        "81CE0002" + "11111111" + "22222222"
        + "80C80006" + "A0B0C0D0" + "0102030405060708" + "0A0B0C0D" + "00000010" + "00001000");

    [Fact]
    public void The_sender_report_is_found_after_another_packet()
    {
        Assert.True(RtcpReader.TryReadSenderReport(Compound, out uint ssrc, out ulong ntp, out uint rtpTime));

        Assert.Equal(0xA0B0C0D0u, ssrc);
        Assert.Equal(0x0102030405060708ul, ntp);
        Assert.Equal(0x0A0B0C0Du, rtpTime);
    }

    [Fact]
    public void A_truncated_compound_or_one_without_a_sender_report_gives_none()
    {
        Assert.False(RtcpReader.TryReadSenderReport(Compound.AsSpan(0, 30), out _, out _, out _));
        Assert.False(RtcpReader.TryReadSenderReport(Compound.AsSpan(0, 12), out _, out _, out _));
    }
}
