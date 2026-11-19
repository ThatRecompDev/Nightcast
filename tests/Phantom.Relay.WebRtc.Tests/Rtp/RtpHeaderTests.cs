// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

/// <summary>RTP headers (RFC 3550) with CSRCs, padding and both header extension forms (RFC 8285), made by hand.</summary>
public sealed class RtpHeaderTests
{
    /// <summary>V=2, marker, PT 102, seq 0x1234, ts 0x01020304, SSRC 0xA1B2C3D4, a three-octet payload.</summary>
    private static readonly byte[] Plain = Convert.FromHexString("80E6123401020304A1B2C3D4" + "AABBCC");

    /// <summary>Two CSRCs, a one-byte extension (id 1: 2 octets, padding, id 3: 1 octet), two octets of padding.</summary>
    private static readonly byte[] Full = Convert.FromHexString(
        "B260FFFF0000000100000002" + "1111111122222222" + "BEDE0002" + "11ABCD00" + "30EE0000" + "77" + "0002");

    /// <summary>A two-byte extension: id 7 with 3 octets, a padding octet, id 9 with no data.</summary>
    private static readonly byte[] TwoByte = Convert.FromHexString("906000010000000100000002" + "10000002" + "0703123456000900" + "55");

    [Fact]
    public void The_fixed_header_reads_its_fields_and_payload()
    {
        var header = new RtpHeader();

        Assert.True(header.TryRead(Plain));

        Assert.True(header.Marker);
        Assert.Equal(102, header.PayloadType);
        Assert.Equal(0x1234, header.SequenceNumber);
        Assert.Equal(0x01020304u, header.Timestamp);
        Assert.Equal(0xA1B2C3D4u, header.Ssrc);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, header.Payload(Plain).ToArray());
    }

    [Fact]
    public void Csrcs_a_one_byte_extension_and_padding_are_read_around_the_payload()
    {
        var header = new RtpHeader();

        Assert.True(header.TryRead(Full));

        Assert.Equal(2, header.CsrcCount);
        Assert.Equal(0x22222222u, header.Csrc(Full, 1));
        Assert.Equal(RtpFormat.OneByteProfile, header.ExtensionProfile);
        Assert.Equal(2, header.PaddingLength);
        Assert.Equal(new byte[] { 0x77 }, header.Payload(Full).ToArray());

        Assert.True(RtpHeaderExtensions.TryFind(Full, header, 1, out var first));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, first.ToArray());
        Assert.True(RtpHeaderExtensions.TryFind(Full, header, 3, out var third));
        Assert.Equal(new byte[] { 0xEE }, third.ToArray());
        Assert.False(RtpHeaderExtensions.TryFind(Full, header, 2, out _));
    }

    [Fact]
    public void Two_byte_extension_elements_are_found_by_id()
    {
        var header = new RtpHeader();

        Assert.True(header.TryRead(TwoByte));

        Assert.True(RtpHeaderExtensions.TryFind(TwoByte, header, 7, out var seven));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56 }, seven.ToArray());
        Assert.True(RtpHeaderExtensions.TryFind(TwoByte, header, 9, out var nine));
        Assert.Equal(0, nine.Length);
        Assert.Equal(new byte[] { 0x55 }, header.Payload(TwoByte).ToArray());
    }

    [Fact]
    public void Malformed_packets_are_refused()
    {
        var header = new RtpHeader();

        Assert.False(header.TryRead(Plain.AsSpan(0, 11)));
        Assert.False(header.TryRead(Convert.FromHexString("40E6123401020304A1B2C3D4")));
        Assert.False(header.TryRead(Full.AsSpan(0, 20)));
        Assert.False(header.TryRead(Convert.FromHexString("A0E6123401020304A1B2C3D4" + "0005")));
        Assert.False(header.TryRead(Convert.FromHexString("A0E6123401020304A1B2C3D4" + "0000")));
        Assert.False(header.TryRead(Convert.FromHexString("9060000100000001000000021000000507031234")));
    }

    [Fact]
    public void The_header_length_is_known_before_the_payload_is_decrypted()
    {
        Assert.True(RtpFormat.TryGetHeaderLength(Full, out int length));

        Assert.Equal(12 + 8 + 4 + 8, length);
    }

    [Fact]
    public void Rtcp_is_told_from_rtp_and_both_from_stun()
    {
        byte[] receiverReport = [0x81, 201, 0, 7, 0, 0, 0, 1, 0, 0, 0, 0];
        byte[] stun = [0, 1, 0, 0, 0x21, 0x12, 0xA4, 0x42, 0, 0, 0, 0];

        Assert.True(RtpFormat.IsRtpOrRtcp(Plain));
        Assert.False(RtpFormat.IsRtcp(Plain));
        Assert.True(RtpFormat.IsRtcp(receiverReport));
        Assert.False(RtpFormat.IsRtpOrRtcp(stun));
    }

    [Fact]
    public void Reading_a_header_and_finding_an_extension_allocates_nothing()
    {
        var header = new RtpHeader();

        var report = AllocationMeter.Measure(() =>
        {
            Assert.True(header.TryRead(Full));
            Assert.True(RtpHeaderExtensions.TryFind(Full, header, 3, out _));
        });

        Assert.Equal(0, report.BytesPerRun);
    }
}
