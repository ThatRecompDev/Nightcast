// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// The agent's one local candidate paired with one remote candidate, and the state of its check: the transaction in
/// flight, how often it was sent and when it is due again. Owned by the agent; made when the checks start, or kept
/// spare until a check arrives from an address no candidate named (a peer-reflexive one, behind a NAT).
/// </summary>
public sealed class IcePair
{
    /// <summary>A pair from our host candidate, or from our relayed one when <paramref name="relayed"/>.</summary>
    public IcePair(IceCandidate remote, ulong priority, bool relayed)
    {
        Endpoint = remote.Endpoint;
        Kind = remote.Kind;
        Priority = priority;
        Relayed = relayed;
    }

    /// <summary>A spare pair, given its remote side by <see cref="Learn"/>.</summary>
    internal IcePair()
    {
    }

    public DatagramEndpoint Endpoint { get; private set; }

    /// <summary>What the remote side's candidate is: as signalled, or peer reflexive when learned from its check.</summary>
    public IceCandidateKind Kind { get; private set; }

    public ulong Priority { get; private set; }

    /// <summary>True when our side of the pair is the TURN relay: its datagrams go through the relay both ways.</summary>
    public bool Relayed { get; private set; }

    public IcePairState State { get; set; }

    /// <summary>The transaction id of the check in flight.</summary>
    public byte[] TransactionId { get; } = new byte[Stun.StunFormat.TransactionIdLength];

    public int Attempts { get; set; }

    /// <summary>When the next transmission is due (<see cref="TimeProvider.GetTimestamp"/> ticks).</summary>
    public long DueAt { get; set; }

    /// <summary>How long to wait for the answer to the last transmission, doubled each time.</summary>
    public TimeSpan Timeout { get; set; }

    /// <summary>When the last check went out (<see cref="TimeProvider.GetTimestamp"/> ticks).</summary>
    public long SentAt { get; set; }

    /// <summary>The time from the last answered check to its answer: the path's round trip, null until one is answered.</summary>
    public TimeSpan? RoundTrip { get; set; }

    /// <summary>Makes a spare pair the one toward a peer-reflexive remote candidate at <paramref name="endpoint"/>.</summary>
    internal void Learn(DatagramEndpoint endpoint, ulong priority, bool relayed)
    {
        Endpoint = endpoint;
        Kind = IceCandidateKind.PeerReflexive;
        Priority = priority;
        Relayed = relayed;
        State = IcePairState.Waiting;
    }
}
