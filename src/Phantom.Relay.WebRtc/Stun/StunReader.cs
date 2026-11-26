// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Hashing;
using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Stun;

/// <summary>
/// Reads STUN messages: recognises one among other datagrams, finds attributes, checks MESSAGE-INTEGRITY and
/// FINGERPRINT, and decodes XOR-MAPPED-ADDRESS. Allocation-free; the spans point into the caller's message.
/// </summary>
public static class StunReader
{
    private const int LengthOffset = 2;
    private const int CookieOffset = 4;
    private const int TransactionOffset = 8;
    private const int PortXorShift = 16;

    /// <summary>
    /// True when <paramref name="datagram"/> is a whole STUN message: top bits zero, the magic cookie, and a length
    /// that matches the datagram and is a multiple of four. DTLS and RTP datagrams never pass.
    /// </summary>
    [HotPath]
    public static bool IsMessage(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < StunFormat.HeaderLength || (datagram[0] & StunFormat.TypeTopBitsMask) != 0)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadUInt16BigEndian(datagram[LengthOffset..]);
        uint cookie = BinaryPrimitives.ReadUInt32BigEndian(datagram[CookieOffset..]);

        return cookie == StunFormat.MagicCookie && length % StunFormat.Alignment == 0 && StunFormat.HeaderLength + length == datagram.Length;
    }

    public static ushort Type(ReadOnlySpan<byte> message)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(message);
    }

    public static ReadOnlySpan<byte> TransactionId(ReadOnlySpan<byte> message)
    {
        return message.Slice(TransactionOffset, StunFormat.TransactionIdLength);
    }

    /// <summary>The first attribute of <paramref name="type"/>: its value (unpadded) and where its header starts.</summary>
    [HotPath]
    public static bool TryFind(ReadOnlySpan<byte> message, ushort type, out ReadOnlySpan<byte> value, out int offset)
    {
        offset = StunFormat.HeaderLength;

        while (offset + StunFormat.AttributeHeaderLength <= message.Length)
        {
            ushort found = BinaryPrimitives.ReadUInt16BigEndian(message[offset..]);
            int length = BinaryPrimitives.ReadUInt16BigEndian(message[(offset + sizeof(ushort))..]);
            int valueStart = offset + StunFormat.AttributeHeaderLength;

            if (valueStart + length > message.Length)
            {
                break;
            }

            if (found == type)
            {
                value = message.Slice(valueStart, length);

                return true;
            }

            offset = valueStart + ((length + StunFormat.Alignment - 1) & ~(StunFormat.Alignment - 1));
        }

        value = default;

        return false;
    }

    /// <summary>True when the message carries MESSAGE-INTEGRITY and it matches <paramref name="integrity"/>'s key.</summary>
    [HotPath]
    public static bool HasValidIntegrity(ReadOnlySpan<byte> message, StunIntegrity integrity)
    {
        if (!TryFind(message, StunFormat.MessageIntegrity, out var mac, out int offset) || mac.Length != StunFormat.IntegrityLength)
        {
            return false;
        }

        ushort lengthThroughIntegrity = (ushort)(offset + StunFormat.AttributeHeaderLength + StunFormat.IntegrityLength - StunFormat.HeaderLength);
        Span<byte> expected = stackalloc byte[StunFormat.IntegrityLength];
        integrity.Compute(message[..offset], lengthThroughIntegrity, expected);

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, mac);
    }

    /// <summary>True when the message ends with a FINGERPRINT that matches its bytes.</summary>
    [HotPath]
    public static bool HasValidFingerprint(ReadOnlySpan<byte> message)
    {
        if (!TryFind(message, StunFormat.Fingerprint, out var value, out int offset) || value.Length != StunFormat.FingerprintLength)
        {
            return false;
        }

        uint expected = Crc32.Hash(message[..offset]) ^ StunFormat.FingerprintXor;

        return BinaryPrimitives.ReadUInt32BigEndian(value) == expected;
    }

    /// <summary>
    /// The ERROR-CODE of an error response (class times 100 plus number, such as 401), or 0 when it carries none.
    /// </summary>
    [HotPath]
    public static int ErrorCode(ReadOnlySpan<byte> message)
    {
        if (!TryFind(message, StunFormat.ErrorCode, out var value, out _) || value.Length <= StunFormat.ErrorNumberOffset)
        {
            return 0;
        }

        return ((value[StunFormat.ErrorClassOffset] & StunFormat.ErrorClassMask) * StunFormat.ErrorClassFactor) + value[StunFormat.ErrorNumberOffset];
    }

    /// <summary>Decodes an IPv4 XOR address value (XOR-MAPPED, XOR-PEER, XOR-RELAYED); false for another family or a short value.</summary>
    [HotPath]
    public static bool TryReadXorMappedAddress(ReadOnlySpan<byte> value, out DatagramEndpoint endpoint)
    {
        endpoint = default;

        if (value.Length < StunFormat.IPv4AddressValueLength || value[1] != StunFormat.FamilyIPv4)
        {
            return false;
        }

        ushort port = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(value[sizeof(ushort)..]) ^ (StunFormat.MagicCookie >> PortXorShift));
        uint address = BinaryPrimitives.ReadUInt32BigEndian(value[sizeof(uint)..]) ^ StunFormat.MagicCookie;
        endpoint = new DatagramEndpoint(address, port);

        return true;
    }
}
