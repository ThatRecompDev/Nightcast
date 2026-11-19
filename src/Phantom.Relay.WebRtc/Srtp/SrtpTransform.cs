// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// One profile's cryptographic transform of one direction: protects and unprotects RTP and RTCP packets in place,
/// given the packet index its caller tracks. Unprotecting checks the tag before it decrypts, and
/// a packet whose tag does not match is left as it came. Every operation is allocation-free.
/// </summary>
public abstract class SrtpTransform
{
    /// <summary>The octets SRTP appends to an RTP packet (the tag).</summary>
    public abstract int RtpOverhead { get; }

    /// <summary>The octets SRTCP appends to an RTCP packet (the E flag and index, and the tag).</summary>
    public abstract int RtcpOverhead { get; }

    /// <summary>The tag's length, which follows (AES-CM) or precedes (AES-GCM) SRTCP's index word.</summary>
    public abstract int TagLength { get; }

    /// <summary>The transform of <paramref name="profile"/> with these session keys.</summary>
    public static SrtpTransform Create(SrtpProfile profile, SrtpSessionKeys keys)
    {
        switch (profile)
        {
            case SrtpProfile.AesCm128HmacSha1Tag80:
                return new SrtpCounterModeTransform(keys);
            case SrtpProfile.AeadAes128Gcm:
                return new SrtpGcmTransform(keys);
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Not an SRTP profile this client supports.");
        }
    }

    /// <summary>
    /// Encrypts the payload of the RTP packet in <paramref name="buffer"/>[..<paramref name="length"/>] (its header is
    /// <paramref name="headerLength"/> octets) and appends the tag; false when the buffer has no room for it.
    /// </summary>
    public abstract bool TryProtectRtp(Span<byte> buffer, int length, int headerLength, uint rolloverCounter, out int protectedLength);

    /// <summary>Checks the tag of an SRTP packet and decrypts its payload in place; false when the tag does not match.</summary>
    public abstract bool TryUnprotectRtp(Span<byte> packet, int headerLength, uint rolloverCounter, out int plainLength);

    /// <summary>Encrypts an RTCP packet in place with SRTCP index <paramref name="index"/> and appends the index and tag.</summary>
    public abstract bool TryProtectRtcp(Span<byte> buffer, int length, uint index, out int protectedLength);

    /// <summary>Checks the tag of an SRTCP packet and decrypts it in place; false when the tag does not match.</summary>
    public abstract bool TryUnprotectRtcp(Span<byte> packet, out int plainLength);
}
