// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// How one message travels: in its stream's order or as soon as it is whole, and how hard it is retried.
/// <paramref name="Limit"/> is the retransmission count or the lifetime in milliseconds, as
/// <paramref name="Reliability"/> says, and unused when it is reliable.
/// </summary>
public readonly record struct SctpSendPolicy(bool Unordered, SctpReliability Reliability, uint Limit)
{
    /// <summary>Ordered and reliable, as TCP would carry it.</summary>
    public static SctpSendPolicy Reliable { get; } = new(false, SctpReliability.Reliable, 0);
}
