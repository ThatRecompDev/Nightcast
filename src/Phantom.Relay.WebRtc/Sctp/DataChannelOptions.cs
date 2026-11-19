// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// What a data channel is (RFC 8831 section 6.4): its label and sub-protocol, whether its messages keep their order,
/// and how hard they are retried (<paramref name="ReliabilityLimit"/> is retransmissions or milliseconds, as
/// <paramref name="Reliability"/> says).
/// </summary>
public sealed record DataChannelOptions(
    string Label,
    string Protocol,
    bool Ordered,
    SctpReliability Reliability,
    uint ReliabilityLimit,
    ushort Priority)
{
    /// <summary>An ordered, reliable channel at normal priority, the browser's default.</summary>
    public static DataChannelOptions Reliable(string label, string protocol)
    {
        return new DataChannelOptions(label, protocol, true, SctpReliability.Reliable, 0, DataChannelProtocol.NormalPriority);
    }

    /// <summary>How each message of the channel travels once the channel is open.</summary>
    public SctpSendPolicy Policy => new(!Ordered, Reliability, ReliabilityLimit);
}
