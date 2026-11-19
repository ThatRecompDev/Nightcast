// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// The session keys of one direction of one SRTP session, for RTP and for RTCP: the cipher key, the salt and (for
/// AES-CM with HMAC-SHA1) the authentication key, which is empty for AES-GCM.
/// </summary>
public sealed record SrtpSessionKeys(byte[] RtpKey, byte[] RtpSalt, byte[] RtpAuthenticationKey, byte[] RtcpKey, byte[] RtcpSalt, byte[] RtcpAuthenticationKey)
{
    /// <summary>The session keys of <paramref name="profile"/> derived from one master key and salt (RFC 3711 section 4.3).</summary>
    public static SrtpSessionKeys Derive(SrtpProfile profile, ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> masterSalt)
    {
        int saltLength = SaltLength(profile);
        int authenticationLength = 0;

        if (profile == SrtpProfile.AesCm128HmacSha1Tag80)
        {
            authenticationLength = SrtpFormat.AuthenticationKeyLength;
        }

        if (masterSalt.Length != saltLength)
        {
            throw new ArgumentException("The master salt does not have the profile's length.", nameof(masterSalt));
        }

        return new SrtpSessionKeys(
            SrtpKeyDerivation.Derive(masterKey, masterSalt, SrtpFormat.RtpEncryptionLabel, SrtpFormat.KeyLength),
            SrtpKeyDerivation.Derive(masterKey, masterSalt, SrtpFormat.RtpSaltLabel, saltLength),
            SrtpKeyDerivation.Derive(masterKey, masterSalt, SrtpFormat.RtpAuthenticationLabel, authenticationLength),
            SrtpKeyDerivation.Derive(masterKey, masterSalt, SrtpFormat.RtcpEncryptionLabel, SrtpFormat.KeyLength),
            SrtpKeyDerivation.Derive(masterKey, masterSalt, SrtpFormat.RtcpSaltLabel, saltLength),
            SrtpKeyDerivation.Derive(masterKey, masterSalt, SrtpFormat.RtcpAuthenticationLabel, authenticationLength));
    }

    /// <summary>The master and session salt length of <paramref name="profile"/>.</summary>
    public static int SaltLength(SrtpProfile profile)
    {
        switch (profile)
        {
            case SrtpProfile.AesCm128HmacSha1Tag80:
                return SrtpFormat.CounterModeSaltLength;
            case SrtpProfile.AeadAes128Gcm:
                return SrtpFormat.GcmSaltLength;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Not an SRTP profile this client supports.");
        }
    }
}
