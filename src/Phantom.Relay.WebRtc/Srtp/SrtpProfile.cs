// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>The SRTP protection profiles a peer may pick in DTLS-SRTP, by their profile numbers (RFC 5764, RFC 7714).</summary>
public enum SrtpProfile
{
    /// <summary>SRTP_AES128_CM_HMAC_SHA1_80: AES-128 counter mode and an 80-bit HMAC-SHA1 tag (RFC 3711).</summary>
    AesCm128HmacSha1Tag80 = 0x0001,

    /// <summary>SRTP_AEAD_AES_128_GCM: AES-128 in Galois/counter mode with a 128-bit tag (RFC 7714).</summary>
    AeadAes128Gcm = 0x0007,
}
