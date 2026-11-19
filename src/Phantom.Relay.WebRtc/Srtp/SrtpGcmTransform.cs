// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Security.Cryptography;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// SRTP_AEAD_AES_128_GCM (RFC 7714): AES-128-GCM with a 96-bit IV of (0, SSRC, ROC, SEQ) or, for SRTCP,
/// (0, SSRC, 0, index), exclusive-or the session salt. SRTP's associated data is the RTP header; SRTCP's is the first
/// header and the sender's SSRC, then the E flag and index word. The tag is 128 bits. GCM is assembled from AES counter
/// mode and <see cref="GaloisHash"/>, both keyed once, so no packet allocates.
/// </summary>
public sealed class SrtpGcmTransform : SrtpTransform
{
    private const int IvSsrcOffset = 2;
    private const int IvRolloverOffset = 6;
    private const int IvSequenceOffset = 10;
    private const int IvRtcpIndexOffset = 8;

    /// <summary>The counter block IV || 1 masks the tag; the data's keystream starts at IV || 2.</summary>
    private const uint FirstDataCounter = 2;

    private readonly AesCounterMode _rtpCipher;
    private readonly AesCounterMode _rtcpCipher;
    private readonly GaloisHash _rtpHash;
    private readonly GaloisHash _rtcpHash;
    private readonly byte[] _rtpSalt;
    private readonly byte[] _rtcpSalt;
    private readonly byte[] _counter = new byte[SrtpFormat.BlockLength];
    private readonly byte[] _tag = new byte[SrtpFormat.BlockLength];
    private readonly byte[] _mask = new byte[SrtpFormat.BlockLength];
    private readonly byte[] _indexWord = new byte[SrtpFormat.RtcpIndexLength];

    public SrtpGcmTransform(SrtpSessionKeys keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        _rtpCipher = new AesCounterMode(keys.RtpKey);
        _rtcpCipher = new AesCounterMode(keys.RtcpKey);
        _rtpHash = new GaloisHash(HashKey(_rtpCipher));
        _rtcpHash = new GaloisHash(HashKey(_rtcpCipher));
        _rtpSalt = keys.RtpSalt;
        _rtcpSalt = keys.RtcpSalt;
    }

    public override int RtpOverhead => SrtpFormat.GcmTagLength;

    public override int RtcpOverhead => SrtpFormat.GcmTagLength + SrtpFormat.RtcpIndexLength;

    public override int TagLength => SrtpFormat.GcmTagLength;

    public override int RtcpIndexFromEnd => SrtpFormat.RtcpIndexLength;

    [HotPath]
    public override bool TryProtectRtp(Span<byte> buffer, int length, int headerLength, uint rolloverCounter, out int protectedLength)
    {
        protectedLength = 0;

        if (headerLength < SrtpFormat.RtpHeaderLength || headerLength > length || length + SrtpFormat.GcmTagLength > buffer.Length)
        {
            return false;
        }

        RtpIv(buffer, rolloverCounter);
        var payload = buffer[headerLength..length];
        Crypt(_rtpCipher, payload);
        RtpTag(buffer[..headerLength], payload);
        _tag.CopyTo(buffer[length..]);
        protectedLength = length + SrtpFormat.GcmTagLength;

        return true;
    }

    [HotPath]
    public override bool TryUnprotectRtp(Span<byte> packet, int headerLength, uint rolloverCounter, out int plainLength)
    {
        plainLength = packet.Length - SrtpFormat.GcmTagLength;

        if (headerLength < SrtpFormat.RtpHeaderLength || plainLength < headerLength)
        {
            return false;
        }

        RtpIv(packet, rolloverCounter);
        var payload = packet[headerLength..plainLength];
        RtpTag(packet[..headerLength], payload);

        if (!CryptographicOperations.FixedTimeEquals(_tag, packet[plainLength..]))
        {
            return false;
        }

        Crypt(_rtpCipher, payload);

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

        // The packet becomes: header and SSRC, ciphertext, tag, then the E flag and index word.
        BinaryPrimitives.WriteUInt32BigEndian(_indexWord, SrtpFormat.RtcpEncryptedFlag | (index & SrtpFormat.RtcpIndexMask));
        RtcpIv(buffer, index & SrtpFormat.RtcpIndexMask);
        var body = buffer[SrtpFormat.RtcpHeaderLength..length];
        Crypt(_rtcpCipher, body);
        RtcpTag(buffer[..SrtpFormat.RtcpHeaderLength], body);

        _tag.CopyTo(buffer[length..]);
        _indexWord.CopyTo(buffer[(length + SrtpFormat.GcmTagLength)..]);
        protectedLength = length + RtcpOverhead;

        return true;
    }

    [HotPath]
    public override bool TryUnprotectRtcp(Span<byte> packet, out int plainLength, out uint index)
    {
        int wordAt = packet.Length - SrtpFormat.RtcpIndexLength;
        plainLength = wordAt - SrtpFormat.GcmTagLength;
        index = 0;

        if (plainLength < SrtpFormat.RtcpHeaderLength)
        {
            return false;
        }

        packet[wordAt..].CopyTo(_indexWord);
        uint word = BinaryPrimitives.ReadUInt32BigEndian(_indexWord);
        bool encrypted = (word & SrtpFormat.RtcpEncryptedFlag) != 0;
        index = word & SrtpFormat.RtcpIndexMask;
        RtcpIv(packet, index);

        // An encrypted packet authenticates its first eight octets; a tagged-only one authenticates all it carries.
        int clearLength = plainLength;

        if (encrypted)
        {
            clearLength = SrtpFormat.RtcpHeaderLength;
        }

        var body = packet[clearLength..plainLength];
        RtcpTag(packet[..clearLength], body);

        if (!CryptographicOperations.FixedTimeEquals(_tag, packet[plainLength..wordAt]))
        {
            return false;
        }

        Crypt(_rtcpCipher, body);

        return true;
    }

    private static byte[] HashKey(AesCounterMode cipher)
    {
        var key = new byte[SrtpFormat.BlockLength];
        cipher.EncryptBlock(new byte[SrtpFormat.BlockLength], key);

        return key;
    }

    private void RtpIv(ReadOnlySpan<byte> packet, uint rolloverCounter)
    {
        var iv = _counter.AsSpan();
        iv.Clear();
        _rtpSalt.CopyTo(iv);

        SrtpBytes.XorWord(iv[IvSsrcOffset..], BinaryPrimitives.ReadUInt32BigEndian(packet[SrtpFormat.RtpSsrcOffset..]));
        SrtpBytes.XorWord(iv[IvRolloverOffset..], rolloverCounter);
        iv[IvSequenceOffset] ^= packet[SrtpFormat.RtpSequenceOffset];
        iv[IvSequenceOffset + 1] ^= packet[SrtpFormat.RtpSequenceOffset + 1];
    }

    private void RtcpIv(ReadOnlySpan<byte> packet, uint index)
    {
        var iv = _counter.AsSpan();
        iv.Clear();
        _rtcpSalt.CopyTo(iv);

        SrtpBytes.XorWord(iv[IvSsrcOffset..], BinaryPrimitives.ReadUInt32BigEndian(packet[SrtpFormat.RtcpSsrcOffset..]));
        SrtpBytes.XorWord(iv[IvRtcpIndexOffset..], index);
    }

    /// <summary>Encrypts or decrypts with the keystream from IV || 2; an empty span is left alone.</summary>
    private void Crypt(AesCounterMode cipher, Span<byte> data)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_counter.AsSpan(SrtpFormat.GcmIvLength), FirstDataCounter);
        cipher.Apply(_counter, data);
    }

    private void RtpTag(ReadOnlySpan<byte> header, ReadOnlySpan<byte> cipherText)
    {
        _rtpHash.Reset();
        _rtpHash.Absorb(header);
        _rtpHash.Pad();
        _rtpHash.Absorb(cipherText);
        _rtpHash.Finish(header.Length, cipherText.Length, _tag);

        Mask(_rtpCipher);
    }

    /// <summary>SRTCP's associated data is the clear part, then the index word; the ciphertext follows.</summary>
    private void RtcpTag(ReadOnlySpan<byte> clear, ReadOnlySpan<byte> cipherText)
    {
        _rtcpHash.Reset();
        _rtcpHash.Absorb(clear);
        _rtcpHash.Absorb(_indexWord);
        _rtcpHash.Pad();
        _rtcpHash.Absorb(cipherText);
        _rtcpHash.Finish(clear.Length + SrtpFormat.RtcpIndexLength, cipherText.Length, _tag);

        Mask(_rtcpCipher);
    }

    /// <summary>The tag is the hash exclusive-or the encrypted counter block IV || 1.</summary>
    private void Mask(AesCounterMode cipher)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_counter.AsSpan(SrtpFormat.GcmIvLength), 1);
        cipher.EncryptBlock(_counter, _mask);
        SrtpBytes.Xor(_tag, _mask);
    }
}
