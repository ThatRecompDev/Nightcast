// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// DTLS-SRTP's keying material (RFC 5764 section 4.2): the TLS exporter with <see cref="ExporterLabel"/> gives
/// client key, server key, client salt, server salt, in that order. The client protects with the client's keys and the
/// server with the server's, so each side unprotects with the other's.
/// </summary>
public static class SrtpKeyingMaterial
{
    /// <summary>The TLS exporter label of DTLS-SRTP.</summary>
    public const string ExporterLabel = "EXTRACTOR-dtls_srtp";

    /// <summary>A key and a salt for each of the two directions.</summary>
    private const int Directions = 2;

    /// <summary>How many octets to ask the exporter for under <paramref name="profile"/>.</summary>
    public static int ExporterLength(SrtpProfile profile)
    {
        return Directions * (SrtpFormat.KeyLength + SrtpSessionKeys.SaltLength(profile));
    }

    /// <summary>Splits the exported material into this side's master key (to protect) and the peer's (to unprotect).</summary>
    public static void Split(SrtpProfile profile, ReadOnlySpan<byte> material, bool localIsClient, out SrtpMasterKey local, out SrtpMasterKey remote)
    {
        if (material.Length != ExporterLength(profile))
        {
            throw new ArgumentException("The keying material does not have the profile's length.", nameof(material));
        }

        int saltLength = SrtpSessionKeys.SaltLength(profile);
        var keys = material[..(Directions * SrtpFormat.KeyLength)];
        var salts = material[(Directions * SrtpFormat.KeyLength)..];
        var client = new SrtpMasterKey(keys[..SrtpFormat.KeyLength].ToArray(), salts[..saltLength].ToArray());
        var server = new SrtpMasterKey(keys[SrtpFormat.KeyLength..].ToArray(), salts[saltLength..].ToArray());

        local = server;
        remote = client;

        if (localIsClient)
        {
            local = client;
            remote = server;
        }
    }
}
