// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// How hard a message is retried (RFC 3758's partial reliability, the policies RFC 8831 offers data channels). A
/// message given up is skipped at the receiver with FORWARD TSN; when the peer does not support it, every message is
/// reliable.
/// </summary>
public enum SctpReliability
{
    /// <summary>Retransmitted until acknowledged.</summary>
    Reliable,

    /// <summary>Given up after <see cref="SctpSendPolicy.Limit"/> retransmissions.</summary>
    MaxRetransmits,

    /// <summary>Given up <see cref="SctpSendPolicy.Limit"/> milliseconds after it was queued, sent or not.</summary>
    Lifetime,
}
