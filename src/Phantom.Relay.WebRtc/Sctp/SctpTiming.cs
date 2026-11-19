// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The association's timers and retry limits (RFC 9260 section 16), tuned for latency where the RFC allows: a home
/// network answers in milliseconds, so a lost packet is retried well before the RFC's one-second floor.
/// </summary>
public static class SctpTiming
{
    /// <summary>The retransmission timeout before a round trip was measured (RFC 9260's RTO.Initial).</summary>
    public static readonly TimeSpan InitialRto = TimeSpan.FromSeconds(1);

    /// <summary>The least retransmission timeout; RFC 9260 suggests one second, WebRTC stacks go far lower.</summary>
    public static readonly TimeSpan MinRto = TimeSpan.FromMilliseconds(200);

    /// <summary>The greatest retransmission timeout; RFC 9260 suggests a minute, too long to stall a channel.</summary>
    public static readonly TimeSpan MaxRto = TimeSpan.FromSeconds(10);

    /// <summary>How long a SACK may wait for a second packet to acknowledge with it (RFC 9260: at most 500 ms).</summary>
    public static readonly TimeSpan DelayedAck = TimeSpan.FromMilliseconds(200);

    /// <summary>How often INIT or COOKIE ECHO is sent again before the handshake gives up (Max.Init.Retransmits).</summary>
    public const int MaxInitRetransmits = 8;

    /// <summary>How many timeouts in a row end the association (Association.Max.Retrans).</summary>
    public const int MaxAssociationRetransmits = 10;
}
