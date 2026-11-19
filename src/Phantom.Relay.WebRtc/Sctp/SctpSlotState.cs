// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>Where a received DATA chunk is.</summary>
internal enum SctpSlotState : byte
{
    /// <summary>No chunk.</summary>
    Empty,

    /// <summary>Received, its bytes held until its message is whole and its turn comes.</summary>
    Held,

    /// <summary>Received and delivered (or skipped); kept only so SACKs still report its TSN until the cumulative TSN passes.</summary>
    Done,
}
