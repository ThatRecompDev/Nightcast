// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>The fixed values of SRTP and SRTCP (RFC 3711) and their AEAD form (RFC 7714).</summary>
public static class SrtpFormat
{
    /// <summary>AES's block, and the counter block of counter mode.</summary>
    public const int BlockLength = 16;

    /// <summary>An AES-128 key.</summary>
    public const int KeyLength = 16;

    /// <summary>The master and session salt of AES counter mode (112 bits).</summary>
    public const int CounterModeSaltLength = 14;

    /// <summary>The master and session salt of AES-GCM (96 bits).</summary>
    public const int GcmSaltLength = 12;

    /// <summary>The HMAC-SHA1 session authentication key (160 bits).</summary>
    public const int AuthenticationKeyLength = 20;

    /// <summary>The whole HMAC-SHA1 output, before it is cut to the tag.</summary>
    public const int Sha1Length = 20;

    /// <summary>The 80-bit tag of SRTP_AES128_CM_HMAC_SHA1_80.</summary>
    public const int HmacTagLength = 10;

    /// <summary>The 128-bit tag of AEAD_AES_128_GCM.</summary>
    public const int GcmTagLength = 16;

    /// <summary>The GCM nonce (IV) is 96 bits; the last 32 bits of the counter block count blocks.</summary>
    public const int GcmIvLength = 12;

    /// <summary>The fixed RTP header: flags, payload type, sequence number, timestamp, SSRC.</summary>
    public const int RtpHeaderLength = 12;

    public const int RtpSequenceOffset = 2;

    public const int RtpSsrcOffset = 8;

    /// <summary>The part of an RTCP packet that SRTCP leaves in the clear: the first header and the sender's SSRC.</summary>
    public const int RtcpHeaderLength = 8;

    public const int RtcpSsrcOffset = 4;

    /// <summary>The SRTCP word after the encrypted part: the E flag and the 31-bit SRTCP index.</summary>
    public const int RtcpIndexLength = 4;

    public const uint RtcpEncryptedFlag = 0x80000000;

    public const uint RtcpIndexMask = 0x7FFFFFFF;

    /// <summary>The sequence number's bits within the 48-bit packet index (ROC * 2^16 + SEQ).</summary>
    public const int SequenceBits = 16;

    public const byte RtpEncryptionLabel = 0x00;

    public const byte RtpAuthenticationLabel = 0x01;

    public const byte RtpSaltLabel = 0x02;

    public const byte RtcpEncryptionLabel = 0x03;

    public const byte RtcpAuthenticationLabel = 0x04;

    public const byte RtcpSaltLabel = 0x05;
}
