// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>
/// The SRTP master keys and salts DTLS exported for each direction (RFC 5764 section 4.2): the client's protect what
/// the client sends, the server's what the server sends.
/// </summary>
public sealed record SrtpKeys(SrtpProfile Profile, byte[] ClientKey, byte[] ClientSalt, byte[] ServerKey, byte[] ServerSalt)
{
    public const string ExporterLabel = "EXTRACTOR-dtls_srtp";

    public const int KeyLength = 16;

    public const int CmSaltLength = 14;

    public const int GcmSaltLength = 12;

    public static int SaltLength(SrtpProfile profile)
    {
        if (profile == SrtpProfile.AeadAes128Gcm)
        {
            return GcmSaltLength;
        }

        return CmSaltLength;
    }

    /// <summary>Splits the exported block: client key, server key, client salt, server salt.</summary>
    public static SrtpKeys Split(SrtpProfile profile, ReadOnlySpan<byte> material)
    {
        int salt = SaltLength(profile);
        var clientKey = material[..KeyLength].ToArray();
        var serverKey = material.Slice(KeyLength, KeyLength).ToArray();
        var clientSalt = material.Slice(KeyLength + KeyLength, salt).ToArray();
        var serverSalt = material.Slice(KeyLength + KeyLength + salt, salt).ToArray();

        return new SrtpKeys(profile, clientKey, clientSalt, serverKey, serverSalt);
    }

    public static int MaterialLength(SrtpProfile profile)
    {
        return (KeyLength + SaltLength(profile)) << 1;
    }
}
