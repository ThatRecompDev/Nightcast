// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>Tells DTLS from RTP/RTCP by the first byte of a datagram (RFC 7983 section 7); STUN is told apart before.</summary>
public static class DatagramClass
{
    private const byte FirstDtls = 20;
    private const byte LastDtls = 63;
    private const byte FirstRtp = 128;
    private const byte LastRtp = 191;

    public static bool IsDtls(ReadOnlySpan<byte> datagram)
    {
        return datagram.Length > 0 && datagram[0] >= FirstDtls && datagram[0] <= LastDtls;
    }

    public static bool IsRtp(ReadOnlySpan<byte> datagram)
    {
        return datagram.Length > 0 && datagram[0] >= FirstRtp && datagram[0] <= LastRtp;
    }
}
