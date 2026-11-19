// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// SRTP_AES128_CM_HMAC_SHA1_80 (RFC 3711): AES-128 counter mode with the IV (salt * 2^16) XOR (SSRC * 2^64) XOR
/// (index * 2^16), and HMAC-SHA1 over the packet and the rollover counter (SRTP) or over the packet and the SRTCP
/// index word (SRTCP), cut to 80 bits. The MACs are keyed once; BouncyCastle's HMAC returns to its keyed state after
/// each tag, so no packet re-keys it.
/// </summary>
public sealed class SrtpCounterModeTransform : SrtpTransform
{
    /// <summary>Where the SSRC is mixed into the IV (bits 64 to 95 of the 128-bit block).</summary>
    private const int IvSsrcOffset = 4;

    /// <summary>Where the 48-bit index is mixed into the IV (bits 16 to 63): the high 32 bits, then the low 16.</summary>
    private const int IvIndexOffset = 8;

    private const int IvSequenceOffset = 12;

    /// <summary>SRTCP's 31-bit index sits in the low 32 of the 48 index bits.</summary>
    private const int IvRtcpIndexOffset = 10;

    private readonly AesCounterMode _rtpCipher;
    private readonly AesCounterMode _rtcpCipher;
    private readonly HMac _rtpMac = new(new Sha1Digest());
    private readonly HMac _rtcpMac = new(new Sha1Digest());
    private readonly byte[] _rtpSalt;
    private readonly byte[] _rtcpSalt;
    private readonly byte[] _iv = new byte[SrtpFormat.BlockLength];
    private readonly byte[] _tag = new byte[SrtpFormat.Sha1Length];

    public SrtpCounterModeTransform(SrtpSessionKeys keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        _rtpCipher = new AesCounterMode(keys.RtpKey);
        _rtcpCipher = new AesCounterMode(keys.RtcpKey);
        _rtpMac.Init(new KeyParameter(keys.RtpAuthenticationKey));
        _rtcpMac.Init(new KeyParameter(keys.RtcpAuthenticationKey));
        _rtpSalt = keys.RtpSalt;
        _rtcpSalt = keys.RtcpSalt;
    }

    public override int RtpOverhead => SrtpFormat.HmacTagLength;

    public override int RtcpOverhead => SrtpFormat.RtcpIndexLength + SrtpFormat.HmacTagLength;

    public override int TagLength => SrtpFormat.HmacTagLength;

    [HotPath]
    public override bool TryProtectRtp(Span<byte> buffer, int length, int headerLength, uint rolloverCounter, out int protectedLength)
    {
        protectedLength = 0;

        if (headerLength < SrtpFormat.RtpHeaderLength || headerLength > length || length + SrtpFormat.HmacTagLength > buffer.Length)
        {
            return false;
        }

        RtpIv(buffer, rolloverCounter);
        _rtpCipher.Apply(_iv, buffer[headerLength..length]);
        RtpTag(buffer[..length], rolloverCounter);
        _tag.AsSpan(0, SrtpFormat.HmacTagLength).CopyTo(buffer[length..]);
        protectedLength = length + SrtpFormat.HmacTagLength;

        return true;
    }

    [HotPath]
    public override bool TryUnprotectRtp(Span<byte> packet, int headerLength, uint rolloverCounter, out int plainLength)
    {
        plainLength = packet.Length - SrtpFormat.HmacTagLength;

        if (headerLength < SrtpFormat.RtpHeaderLength || plainLength < headerLength)
        {
            return false;
        }

        RtpTag(packet[..plainLength], rolloverCounter);

        if (!CryptographicOperations.FixedTimeEquals(_tag.AsSpan(0, SrtpFormat.HmacTagLength), packet[plainLength..]))
        {
            return false;
        }

        RtpIv(packet, rolloverCounter);
        _rtpCipher.Apply(_iv, packet[headerLength..plainLength]);

        return true;
    }

    [HotPath]
    public override bool TryProtectRtcp(Span<byte> buffer, int length, uint index, out int protectedLength)
    {
        protectedLength = 0;

        if (length < SrtpFormat.RtcpHeaderLength || length + RtcpOverhead > buffer.Length)
        {
            return false;
        }

        // Encrypt everything after the first header and the sender's SSRC, then append E and the index, then the tag.
        RtcpIv(buffer, index & SrtpFormat.RtcpIndexMask);
        _rtcpCipher.Apply(_iv, buffer[SrtpFormat.RtcpHeaderLength..length]);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[length..], SrtpFormat.RtcpEncryptedFlag | (index & SrtpFormat.RtcpIndexMask));

        int authenticated = length + SrtpFormat.RtcpIndexLength;
        RtcpTag(buffer[..authenticated]);
        _tag.AsSpan(0, SrtpFormat.HmacTagLength).CopyTo(buffer[authenticated..]);
        protectedLength = authenticated + SrtpFormat.HmacTagLength;

        return true;
    }

    [HotPath]
    public override bool TryUnprotectRtcp(Span<byte> packet, out int plainLength)
    {
        int authenticated = packet.Length - SrtpFormat.HmacTagLength;
        plainLength = authenticated - SrtpFormat.RtcpIndexLength;

        if (plainLength < SrtpFormat.RtcpHeaderLength)
        {
            return false;
        }

        RtcpTag(packet[..authenticated]);

        if (!CryptographicOperations.FixedTimeEquals(_tag.AsSpan(0, SrtpFormat.HmacTagLength), packet[authenticated..]))
        {
            return false;
        }

        uint word = BinaryPrimitives.ReadUInt32BigEndian(packet[plainLength..]);

        if ((word & SrtpFormat.RtcpEncryptedFlag) != 0)
        {
            RtcpIv(packet, word & SrtpFormat.RtcpIndexMask);
            _rtcpCipher.Apply(_iv, packet[SrtpFormat.RtcpHeaderLength..plainLength]);
        }

        return true;
    }

    private void RtpIv(ReadOnlySpan<byte> packet, uint rolloverCounter)
    {
        var iv = _iv.AsSpan();
        iv.Clear();
        _rtpSalt.CopyTo(iv);

        SrtpBytes.XorWord(iv[IvSsrcOffset..], BinaryPrimitives.ReadUInt32BigEndian(packet[SrtpFormat.RtpSsrcOffset..]));
        SrtpBytes.XorWord(iv[IvIndexOffset..], rolloverCounter);
        iv[IvSequenceOffset] ^= packet[SrtpFormat.RtpSequenceOffset];
        iv[IvSequenceOffset + 1] ^= packet[SrtpFormat.RtpSequenceOffset + 1];
    }

    private void RtcpIv(ReadOnlySpan<byte> packet, uint index)
    {
        var iv = _iv.AsSpan();
        iv.Clear();
        _rtcpSalt.CopyTo(iv);

        SrtpBytes.XorWord(iv[IvSsrcOffset..], BinaryPrimitives.ReadUInt32BigEndian(packet[SrtpFormat.RtcpSsrcOffset..]));
        SrtpBytes.XorWord(iv[IvRtcpIndexOffset..], index);
    }

    /// <summary>HMAC-SHA1 over the authenticated portion and the rollover counter (RFC 3711 section 4.2).</summary>
    private void RtpTag(ReadOnlySpan<byte> authenticated, uint rolloverCounter)
    {
        Span<byte> counter = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(counter, rolloverCounter);

        _rtpMac.BlockUpdate(authenticated);
        _rtpMac.BlockUpdate(counter);
        _rtpMac.DoFinal(_tag);
    }

    private void RtcpTag(ReadOnlySpan<byte> authenticated)
    {
        _rtcpMac.BlockUpdate(authenticated);
        _rtcpMac.DoFinal(_tag);
    }
}
