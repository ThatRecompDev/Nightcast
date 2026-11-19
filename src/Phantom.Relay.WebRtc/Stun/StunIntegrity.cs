// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Stun;

/// <summary>
/// MESSAGE-INTEGRITY with one ICE short-term credential: HMAC-SHA1 keyed with the password's UTF-8 bytes (RFC 8489
/// section 14.5). One instance per credential, reused for every message; computing allocates nothing.
/// </summary>
public sealed class StunIntegrity
{
    private const int LengthOffset = 2;

    private readonly HMac _mac = new(new Sha1Digest());
    private readonly KeyParameter _key;

    public StunIntegrity(ReadOnlySpan<byte> key)
    {
        _key = new KeyParameter(key);
    }

    public static StunIntegrity ForPassword(string password)
    {
        return new StunIntegrity(System.Text.Encoding.UTF8.GetBytes(password));
    }

    /// <summary>
    /// The HMAC over <paramref name="before"/> (the message up to the MESSAGE-INTEGRITY attribute), as if the header's
    /// length ended right after that attribute (<paramref name="lengthThroughIntegrity"/>).
    /// </summary>
    [HotPath]
    public void Compute(ReadOnlySpan<byte> before, ushort lengthThroughIntegrity, Span<byte> destination)
    {
        Span<byte> length = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(length, lengthThroughIntegrity);
        _mac.Init(_key);

        _mac.BlockUpdate(before[..LengthOffset]);
        _mac.BlockUpdate(length);
        _mac.BlockUpdate(before[(LengthOffset + sizeof(ushort))..]);
        _mac.DoFinal(destination);
    }
}
