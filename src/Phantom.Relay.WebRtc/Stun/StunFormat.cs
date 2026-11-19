// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Stun;

/// <summary>The fixed values of STUN (RFC 8489) and its ICE attributes (RFC 8445).</summary>
public static class StunFormat
{
    public const int HeaderLength = 20;

    public const int TransactionIdLength = 12;

    public const uint MagicCookie = 0x2112A442;

    public const int AttributeHeaderLength = 4;

    /// <summary>Attributes are padded to a multiple of four bytes.</summary>
    public const int Alignment = 4;

    public const int IntegrityLength = 20;

    public const int FingerprintLength = 4;

    /// <summary>The fingerprint is the CRC-32 of the message so far, exclusive-or this value ("STUN").</summary>
    public const uint FingerprintXor = 0x5354554E;

    public const ushort BindingRequest = 0x0001;

    public const ushort BindingSuccess = 0x0101;

    public const ushort BindingError = 0x0111;

    public const ushort Username = 0x0006;

    public const ushort MessageIntegrity = 0x0008;

    public const ushort ErrorCode = 0x0009;

    public const ushort XorMappedAddress = 0x0020;

    public const ushort Priority = 0x0024;

    public const ushort UseCandidate = 0x0025;

    public const ushort Software = 0x8022;

    public const ushort Fingerprint = 0x8028;

    public const ushort IceControlled = 0x8029;

    public const ushort IceControlling = 0x802A;

    /// <summary>The address family of an IPv4 (XOR-)MAPPED-ADDRESS.</summary>
    public const byte FamilyIPv4 = 0x01;

    public const int IPv4AddressValueLength = 8;

    /// <summary>The two most significant bits of every STUN message are zero.</summary>
    public const byte TypeTopBitsMask = 0xC0;

    /// <summary>The longest message this codec writes or reads: what fits an unfragmented datagram on any path.</summary>
    public const int MaxMessageLength = 548;
}
