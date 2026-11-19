// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// One side's ICE short-term credentials (RFC 8445 section 5.3): the username fragment and the password, as their
/// SDP attributes carry them (a=ice-ufrag, a=ice-pwd).
/// </summary>
public sealed record IceCredentials(string Ufrag, string Password)
{
    /// <summary>At least 24 bits of randomness for the fragment (RFC 8445): 4 bytes, 8 hex digits.</summary>
    private const int UfragBytes = 4;

    /// <summary>At least 128 bits for the password (RFC 8445): 16 bytes, 32 hex digits.</summary>
    private const int PasswordBytes = 16;

    public static IceCredentials Create(IRandomSource random)
    {
        return new IceCredentials(Hex(random, UfragBytes), Hex(random, PasswordBytes));
    }

    private static string Hex(IRandomSource random, int length)
    {
        Span<byte> bytes = stackalloc byte[length];

        for (int i = 0; i < length; i++)
        {
            bytes[i] = (byte)random.NextUInt32();
        }

        return Convert.ToHexStringLower(bytes);
    }
}
