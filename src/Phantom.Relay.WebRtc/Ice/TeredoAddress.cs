// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// A Teredo IPv6 address (RFC 4380 section 4) read back into the IPv4 endpoint it was made from. The address is
/// <c>2001:0000</c>, the Teredo server's IPv4 address (32 bits), flags (16), the client's external UDP port (16) and its
/// external IPv4 address (32), the last two with every bit inverted. Away from home a console offers such a candidate:
/// it names the public IPv4 address and port of the console's router, which an IPv4-only client can reach directly.
/// </summary>
public static class TeredoAddress
{
    private const int IPv6Length = 16;
    private const uint Prefix = 0x2001_0000;
    private const int PortOffset = 10;
    private const int ClientOffset = 12;
    private const ushort PortMask = 0xFFFF;
    private const uint ClientMask = 0xFFFF_FFFF;

    /// <summary>
    /// The external IPv4 address and port a Teredo address carries; false for any text that is not a Teredo address.
    /// </summary>
    public static bool TryRead(string text, out uint address, out ushort port)
    {
        address = 0;
        port = 0;

        if (!IPAddress.TryParse(text, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[IPv6Length];

        if (!parsed.TryWriteBytes(bytes, out _) || BinaryPrimitives.ReadUInt32BigEndian(bytes) != Prefix)
        {
            return false;
        }

        port = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[PortOffset..]) ^ PortMask);
        address = BinaryPrimitives.ReadUInt32BigEndian(bytes[ClientOffset..]) ^ ClientMask;

        return true;
    }
}
