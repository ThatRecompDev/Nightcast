// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// The agent's one local candidate paired with one remote candidate, and the state of its check: the transaction in
/// flight, how often it was sent and when it is due again. Owned by the agent; made when the checks start.
/// </summary>
public sealed class IcePair
{
    public IcePair(IceCandidate remote, ulong priority)
    {
        Remote = remote;
        Priority = priority;
    }

    public IceCandidate Remote { get; }

    public ulong Priority { get; }

    public DatagramEndpoint Endpoint => Remote.Endpoint;

    public IcePairState State { get; set; }

    /// <summary>The transaction id of the check in flight.</summary>
    public byte[] TransactionId { get; } = new byte[Stun.StunFormat.TransactionIdLength];

    public int Attempts { get; set; }

    /// <summary>When the next transmission is due (<see cref="TimeProvider.GetTimestamp"/> ticks).</summary>
    public long DueAt { get; set; }

    /// <summary>How long to wait for the answer to the last transmission, doubled each time.</summary>
    public TimeSpan Timeout { get; set; }
}
