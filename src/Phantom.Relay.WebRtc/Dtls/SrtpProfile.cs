// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>The SRTP protection profiles DTLS-SRTP negotiates (RFC 5764, RFC 7714), with their key and salt sizes.</summary>
public enum SrtpProfile
{
    /// <summary>AES-128 counter mode, 80-bit HMAC-SHA1 tag: 16-byte key, 14-byte salt.</summary>
    AesCm128HmacSha1_80,

    /// <summary>AES-128 GCM: 16-byte key, 12-byte salt.</summary>
    AeadAes128Gcm,
}
