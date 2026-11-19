// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Crypto.Digests;

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>
/// The SHA-256 certificate fingerprint of RFC 8122 as SDP carries it (a=fingerprint:sha-256 AB:CD:...), computed and
/// read from a session description.
/// </summary>
public static class DtlsFingerprint
{
    public const string Algorithm = "sha-256";

    private const string Attribute = "a=fingerprint:";
    private const int DigestLength = 32;
    private const int HexDigitsPerByte = 2;

    /// <summary>"sha-256" and the hex pairs: the attribute's two fields.</summary>
    private const int Fields = 2;

    public static string Of(ReadOnlySpan<byte> certificate)
    {
        var digest = new Sha256Digest();
        Span<byte> hash = stackalloc byte[DigestLength];
        digest.BlockUpdate(certificate);
        digest.DoFinal(hash);

        return string.Join(':', Convert.ToHexString(hash).Chunk(HexDigitsPerByte).Select(static pair => new string(pair)));
    }

    /// <summary>The first sha-256 fingerprint in a description, upper-cased; null when it carries none.</summary>
    public static string? Read(string sdp)
    {
        foreach (string raw in sdp.Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (!line.StartsWith(Attribute, StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line[Attribute.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == Fields && parts[0].Equals(Algorithm, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].ToUpperInvariant();
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="certificate"/>'s fingerprint is <paramref name="expected"/> (case ignored).</summary>
    public static bool Matches(ReadOnlySpan<byte> certificate, string expected)
    {
        return string.Equals(Of(certificate), expected, StringComparison.OrdinalIgnoreCase);
    }
}
