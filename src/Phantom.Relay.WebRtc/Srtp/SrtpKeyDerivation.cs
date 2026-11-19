// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// SRTP's key derivation (RFC 3711 section 4.3) with a key derivation rate of zero, as DTLS-SRTP uses it: each session
/// key is the AES-CM keystream under the master key, started at the master salt exclusive-or the key's label. The label
/// sits in the seventh octet of the 14-octet salt; AES-GCM's 12-octet master salt is padded on the right (RFC 7714
/// section 11), which puts the label in the same place.
/// </summary>
public static class SrtpKeyDerivation
{
    /// <summary>Where the label lands: the salt is 112 bits, the label and the 48-bit index (zero) its last 56.</summary>
    private const int LabelOffset = 7;

    /// <summary>Fills <paramref name="destination"/> with the session key of <paramref name="label"/>.</summary>
    public static void Derive(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> masterSalt, byte label, Span<byte> destination)
    {
        if (masterKey.Length != SrtpFormat.KeyLength || masterSalt.Length > SrtpFormat.CounterModeSaltLength)
        {
            throw new ArgumentException("SRTP needs a 128-bit master key and at most a 112-bit master salt.");
        }

        Span<byte> counter = stackalloc byte[SrtpFormat.BlockLength];
        counter.Clear();
        masterSalt.CopyTo(counter);
        counter[LabelOffset] ^= label;

        destination.Clear();
        new AesCounterMode(masterKey).Apply(counter, destination);
    }

    /// <summary>A session key of <paramref name="length"/> octets.</summary>
    public static byte[] Derive(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> masterSalt, byte label, int length)
    {
        var key = new byte[length];
        Derive(masterKey, masterSalt, label, key.AsSpan());

        return key;
    }
}
