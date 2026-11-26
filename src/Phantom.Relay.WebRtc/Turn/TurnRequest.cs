// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>One TURN request in flight: its transaction, how often it was sent and when it is due again.</summary>
public sealed class TurnRequest
{
    public byte[] TransactionId { get; } = new byte[Stun.StunFormat.TransactionIdLength];

    /// <summary>True from the request's first transmission until its answer or its last attempt.</summary>
    public bool Active { get; set; }

    public int Attempts { get; set; }

    /// <summary>When the next transmission is due (<see cref="TimeProvider.GetTimestamp"/> ticks).</summary>
    public long DueAt { get; set; }

    public TimeSpan Timeout { get; set; }
}
