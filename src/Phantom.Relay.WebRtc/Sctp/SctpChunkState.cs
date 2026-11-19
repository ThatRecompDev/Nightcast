// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>Where an outgoing DATA chunk is.</summary>
internal enum SctpChunkState : byte
{
    /// <summary>No chunk: acknowledged and released, or never used.</summary>
    Free,

    /// <summary>Queued, never sent.</summary>
    Queued,

    /// <summary>Sent and counted in the bytes in flight.</summary>
    InFlight,

    /// <summary>Lost (a timeout or three miss reports): sent again before anything new.</summary>
    Retransmit,

    /// <summary>Reported received in a gap block, waiting for the cumulative acknowledgement.</summary>
    GapAcked,

    /// <summary>Given up by its message's policy; FORWARD TSN tells the peer to skip it.</summary>
    Abandoned,
}
