// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// The fixed values of the RTP header (RFC 3550 section 5.1) and its header extensions (RFC 8285), and the two
/// questions every received datagram asks first: is it RTP or RTCP, and how long is its header.
/// </summary>
public static class RtpFormat
{
    public const int Version = 2;

    public const int FixedHeaderLength = 12;

    public const int SequenceOffset = 2;

    public const int TimestampOffset = 4;

    public const int SsrcOffset = 8;

    public const int CsrcLength = 4;

    /// <summary>The extension header: a 16-bit profile and a 16-bit length in 32-bit words.</summary>
    public const int ExtensionHeaderLength = 4;

    public const int WordLength = 4;

    public const int VersionShift = 6;

    public const int PaddingBit = 0x20;

    public const int ExtensionBit = 0x10;

    public const int CsrcCountMask = 0x0F;

    public const int MarkerBit = 0x80;

    public const int PayloadTypeMask = 0x7F;

    /// <summary>The profile of one-byte header extensions (RFC 8285 section 4.2).</summary>
    public const ushort OneByteProfile = 0xBEDE;

    /// <summary>The profile of two-byte header extensions is 0x100 followed by four application bits (section 4.3).</summary>
    public const ushort TwoByteProfile = 0x1000;

    public const ushort TwoByteProfileMask = 0xFFF0;

    /// <summary>The largest RTP packet this client expects in one datagram (an Ethernet MTU's payload).</summary>
    public const int MaxPacketLength = 1500;

    /// <summary>RTCP's packet types 192 to 223 share the second octet with RTP's marker and payload type (RFC 5761).</summary>
    public const int FirstRtcpPacketType = 192;

    public const int LastRtcpPacketType = 223;

    /// <summary>RTP and RTCP datagrams start with an octet in 128..191 (RFC 7983); STUN and DTLS never do.</summary>
    public const int FirstRtpOctet = 128;

    public const int LastRtpOctet = 191;

    /// <summary>True when a datagram is RTP or RTCP rather than STUN or DTLS (RFC 7983 section 7).</summary>
    [HotPath]
    public static bool IsRtpOrRtcp(ReadOnlySpan<byte> datagram)
    {
        return datagram.Length >= FixedHeaderLength && datagram[0] >= FirstRtpOctet && datagram[0] <= LastRtpOctet;
    }

    /// <summary>True when an RTP-or-RTCP datagram is RTCP, by its packet type (RFC 5761 section 4).</summary>
    [HotPath]
    public static bool IsRtcp(ReadOnlySpan<byte> datagram)
    {
        return datagram.Length > 1 && datagram[1] >= FirstRtcpPacketType && datagram[1] <= LastRtcpPacketType;
    }

    /// <summary>
    /// The length of a packet's header, CSRCs and header extension included: what SRTP leaves in the clear. Padding is
    /// not looked at, as it is encrypted until the packet is unprotected.
    /// </summary>
    [HotPath]
    public static bool TryGetHeaderLength(ReadOnlySpan<byte> packet, out int headerLength)
    {
        headerLength = 0;

        if (packet.Length < FixedHeaderLength || packet[0] >> VersionShift != Version)
        {
            return false;
        }

        int length = FixedHeaderLength + ((packet[0] & CsrcCountMask) * CsrcLength);

        if ((packet[0] & ExtensionBit) != 0)
        {
            if (length + ExtensionHeaderLength > packet.Length)
            {
                return false;
            }

            int words = BinaryPrimitives.ReadUInt16BigEndian(packet[(length + sizeof(ushort))..]);
            length += ExtensionHeaderLength + (words * WordLength);
        }

        if (length > packet.Length)
        {
            return false;
        }

        headerLength = length;

        return true;
    }
}
