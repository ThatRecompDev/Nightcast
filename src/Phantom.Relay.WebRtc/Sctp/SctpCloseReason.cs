// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>Why an association is closed.</summary>
public enum SctpCloseReason
{
    /// <summary>It has not ended (it may not have started).</summary>
    None,

    /// <summary>A graceful shutdown, asked for by either side, completed.</summary>
    Shutdown,

    /// <summary>The peer sent ABORT.</summary>
    AbortedByPeer,

    /// <summary>This side called <see cref="SctpAssociation.Abort"/>.</summary>
    Aborted,

    /// <summary>The peer stopped answering: the handshake, a shutdown or data ran out of retransmissions.</summary>
    TimedOut,
}
