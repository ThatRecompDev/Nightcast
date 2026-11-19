// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Srtp;

/// <summary>
/// SRTP_AES128_CM_HMAC_SHA1_80 against RFC 3711's formulas worked out here from the bare AES block cipher and HMAC-SHA1,
/// so the transform's IV layout, keystream and tag are checked by a second, plainer implementation.
/// </summary>
public sealed class SrtpCounterModeTransformTests
{
    private const int HeaderLength = 12;
    private const uint Ssrc = 0xCAFEBABE;
    private const ushort Sequence = 0xBEEF;
    private const uint Rollover = 0x00000102;

    private static readonly byte[] MasterKey = Convert.FromHexString("E1F97A0D3E018BE0D64FA32C06DE4139");
    private static readonly byte[] MasterSalt = Convert.FromHexString("0EC675AD498AFEEBB6960B3AABE6");
    private static readonly SrtpSessionKeys Keys = SrtpSessionKeys.Derive(SrtpProfile.AesCm128HmacSha1Tag80, MasterKey, MasterSalt);

    [Fact]
    public void Protecting_rtp_encrypts_the_payload_and_appends_the_80_bit_tag_of_rfc_3711()
    {
        var plain = RtpPacket(37);
        var buffer = new byte[plain.Length + SrtpFormat.HmacTagLength];
        plain.CopyTo(buffer, 0);

        Assert.True(Transform().TryProtectRtp(buffer, plain.Length, HeaderLength, Rollover, out int length));

        Assert.Equal(Convert.ToHexString(ExpectedSrtp(plain)), Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void Unprotecting_gives_the_rtp_packet_back_and_refuses_a_changed_octet()
    {
        var plain = RtpPacket(1100);
        var srtp = ExpectedSrtp(plain);
        var tampered = (byte[])srtp.Clone();
        tampered[600] ^= 0x10;
        var transform = Transform();

        Assert.True(transform.TryUnprotectRtp(srtp, HeaderLength, Rollover, out int length));
        Assert.Equal(plain, srtp.AsSpan(0, length).ToArray());
        Assert.False(transform.TryUnprotectRtp(tampered, HeaderLength, Rollover, out _));
        Assert.False(transform.TryUnprotectRtp(ExpectedSrtp(plain), HeaderLength, Rollover + 1, out _));
    }

    [Fact]
    public void Protecting_rtcp_encrypts_after_the_ssrc_and_appends_the_index_and_tag()
    {
        byte[] rtcp = [0x81, 0xC9, 0x00, 0x07, 0x11, 0x22, 0x33, 0x44, .. Enumerable.Range(0, 24).Select(i => (byte)i)];
        var buffer = new byte[rtcp.Length + 14];
        rtcp.CopyTo(buffer, 0);

        Assert.True(Transform().TryProtectRtcp(buffer, rtcp.Length, 0x1234, out int length));

        Assert.Equal(Convert.ToHexString(ExpectedSrtcp(rtcp, 0x1234)), Convert.ToHexString(buffer.AsSpan(0, length)));
        Assert.True(Transform().TryUnprotectRtcp(buffer.AsSpan(0, length), out int plainLength));
        Assert.Equal(rtcp, buffer.AsSpan(0, plainLength).ToArray());
    }

    [Fact]
    public void An_srtcp_packet_with_a_wrong_tag_is_refused_and_left_as_it_was()
    {
        byte[] rtcp = [0x81, 0xC9, 0x00, 0x01, 0x11, 0x22, 0x33, 0x44];
        var srtcp = ExpectedSrtcp(rtcp, 7);
        srtcp[^1] ^= 1;
        var before = (byte[])srtcp.Clone();

        Assert.False(Transform().TryUnprotectRtcp(srtcp, out _));

        Assert.Equal(before, srtcp);
    }

    [Fact]
    public void Derived_keys_have_the_profile_lengths()
    {
        Assert.Equal(16, Keys.RtpKey.Length);
        Assert.Equal(14, Keys.RtpSalt.Length);
        Assert.Equal(20, Keys.RtpAuthenticationKey.Length);
        Assert.Equal(Convert.FromHexString("C61E7A93744F39EE10734AFE3FF7A087"), Keys.RtpKey);
        Assert.NotEqual(Keys.RtpKey, Keys.RtcpKey);
    }

    private static SrtpTransform Transform()
    {
        return SrtpTransform.Create(SrtpProfile.AesCm128HmacSha1Tag80, Keys);
    }

    private static byte[] RtpPacket(int payloadLength)
    {
        var packet = new byte[HeaderLength + payloadLength];
        packet[0] = 0x80;
        packet[1] = 102;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), Sequence);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), 90000);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8), Ssrc);

        for (int i = HeaderLength; i < packet.Length; i++)
        {
            packet[i] = (byte)(i * 7);
        }

        return packet;
    }

    /// <summary>RFC 3711 by hand: IV = salt*2^16 ^ SSRC*2^64 ^ index*2^16, then HMAC-SHA1(packet || ROC) cut to 80 bits.</summary>
    private static byte[] ExpectedSrtp(byte[] plain)
    {
        var iv = new byte[16];
        Keys.RtpSalt.CopyTo(iv, 0);
        Xor(iv, 4, Ssrc);
        Xor(iv, 8, Rollover);
        Xor(iv, 10, Sequence);

        var output = (byte[])plain.Clone();
        ApplyKeystream(Keys.RtpKey, iv, output.AsSpan(HeaderLength));
        var roc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(roc, Rollover);
        var tag = HmacSha1(Keys.RtpAuthenticationKey, [.. output, .. roc]);

        return [.. output, .. tag.AsSpan(0, 10)];
    }

    private static byte[] ExpectedSrtcp(byte[] rtcp, uint index)
    {
        var iv = new byte[16];
        Keys.RtcpSalt.CopyTo(iv, 0);
        Xor(iv, 4, BinaryPrimitives.ReadUInt32BigEndian(rtcp.AsSpan(4)));
        Xor(iv, 10, index);

        var output = (byte[])rtcp.Clone();
        ApplyKeystream(Keys.RtcpKey, iv, output.AsSpan(8));
        var word = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, 0x80000000 | index);
        byte[] authenticated = [.. output, .. word];

        return [.. authenticated, .. HmacSha1(Keys.RtcpAuthenticationKey, authenticated).AsSpan(0, 10)];
    }

    private static void ApplyKeystream(byte[] key, byte[] iv, Span<byte> data)
    {
        var aes = AesUtilities.CreateEngine();
        aes.Init(true, new KeyParameter(key));
        var counter = (byte[])iv.Clone();
        var keystream = new byte[16];

        for (int offset = 0, block = 0; offset < data.Length; offset += 16, block++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(counter.AsSpan(14), (ushort)block);
            aes.ProcessBlock(counter, 0, keystream, 0);

            for (int i = 0; i < 16 && offset + i < data.Length; i++)
            {
                data[offset + i] ^= keystream[i];
            }
        }
    }

    private static byte[] HmacSha1(byte[] key, byte[] data)
    {
        var mac = new HMac(new Sha1Digest());
        mac.Init(new KeyParameter(key));
        mac.BlockUpdate(data, 0, data.Length);
        var output = new byte[20];
        mac.DoFinal(output, 0);

        return output;
    }

    private static void Xor(byte[] iv, int offset, uint value)
    {
        var span = iv.AsSpan(offset, 4);
        BinaryPrimitives.WriteUInt32BigEndian(span, BinaryPrimitives.ReadUInt32BigEndian(span) ^ value);
    }
}
