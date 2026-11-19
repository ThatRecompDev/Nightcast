// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Srtp;

/// <summary>The AEAD_AES_128_GCM vectors of RFC 7714: SRTP (sections 16.1.1 and 16.1.2) and SRTCP (17.1), as data.</summary>
public sealed class Rfc7714VectorTests
{
    private const int RtpHeaderLength = 12;

    private static readonly byte[] Key = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
    private static readonly byte[] Salt = Convert.FromHexString("517569642070726f2071756f");

    private static readonly byte[] RtpPacket = Convert.FromHexString(
        "8040f17b8041f8d35501a0b2" + "47616c6c696120657374206f6d6e6973"
        + "2064697669736120696e207061727465" + "732074726573");

    private static readonly byte[] SrtpPacket = Convert.FromHexString(
        "8040f17b8041f8d35501a0b2f24de3a3fb34de6cacba861c9d7e4bcabe633bd50d294e6f42a5f47a51c7d19b36de3adf8833"
        + "899d7f27beb16a9152cf765ee4390cce");

    private static readonly byte[] RtcpPacket = Convert.FromHexString(
        "81c8000d4d6172734e5450314e54503252545020" + "0000042a0000e9304c756e61deadbeefdeadbeefdeadbeefdeadbeefdeadbeef");

    private static readonly byte[] SrtcpPacket = Convert.FromHexString(
        "81c8000d4d61727363e94885dcdab67ca727d7662f6b7e997ff5c0f76c06f32dc676a5f1730d6fda4ce09b4686303ded"
        + "0bb9275bc84aa45896cf4d2fc5abf87245d9eade800005d4");

    [Fact]
    public void The_rtp_packet_encrypts_to_the_packet_of_16_1_1()
    {
        var buffer = new byte[RtpPacket.Length + SrtpFormat.GcmTagLength];
        RtpPacket.CopyTo(buffer, 0);

        Assert.True(Transform().TryProtectRtp(buffer, RtpPacket.Length, RtpHeaderLength, 0, out int length));

        Assert.Equal(Convert.ToHexString(SrtpPacket), Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void The_srtp_packet_of_16_1_2_decrypts_to_the_rtp_packet()
    {
        var packet = (byte[])SrtpPacket.Clone();

        Assert.True(Transform().TryUnprotectRtp(packet, RtpHeaderLength, 0, out int length));

        Assert.Equal(Convert.ToHexString(RtpPacket), Convert.ToHexString(packet.AsSpan(0, length)));
    }

    [Fact]
    public void A_changed_header_octet_or_another_rollover_counter_fails_the_tag()
    {
        var tampered = (byte[])SrtpPacket.Clone();
        tampered[1] ^= 1;
        var transform = Transform();

        Assert.False(transform.TryUnprotectRtp(tampered, RtpHeaderLength, 0, out _));
        Assert.False(transform.TryUnprotectRtp((byte[])SrtpPacket.Clone(), RtpHeaderLength, 1, out _));
    }

    [Fact]
    public void The_rtcp_packet_encrypts_to_the_packet_of_17_1()
    {
        var buffer = new byte[RtcpPacket.Length + 20];
        RtcpPacket.CopyTo(buffer, 0);

        Assert.True(Transform().TryProtectRtcp(buffer, RtcpPacket.Length, 0x5d4, out int length));

        Assert.Equal(Convert.ToHexString(SrtcpPacket), Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void The_srtcp_packet_of_17_1_decrypts_to_the_rtcp_packet()
    {
        var packet = (byte[])SrtcpPacket.Clone();

        Assert.True(Transform().TryUnprotectRtcp(packet, out int length, out _));

        Assert.Equal(Convert.ToHexString(RtcpPacket), Convert.ToHexString(packet.AsSpan(0, length)));
    }

    [Fact]
    public void A_changed_srtcp_index_fails_the_tag()
    {
        var tampered = (byte[])SrtcpPacket.Clone();
        tampered[^1] ^= 1;

        Assert.False(Transform().TryUnprotectRtcp(tampered, out _, out _));
    }

    [Fact]
    public void A_buffer_without_room_for_the_tag_is_refused()
    {
        var buffer = (byte[])RtpPacket.Clone();

        Assert.False(Transform().TryProtectRtp(buffer, buffer.Length, RtpHeaderLength, 0, out _));
        Assert.Equal(RtpPacket, buffer);
    }

    private static SrtpTransform Transform()
    {
        var keys = new SrtpSessionKeys(Key, Salt, [], Key, Salt, []);

        return SrtpTransform.Create(SrtpProfile.AeadAes128Gcm, keys);
    }
}
