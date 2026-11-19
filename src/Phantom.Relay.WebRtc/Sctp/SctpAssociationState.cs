// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>Where an association is (RFC 9260 section 4).</summary>
public enum SctpAssociationState
{
    /// <summary>No association: not started, or ended (<see cref="SctpAssociation.CloseReason"/> says how).</summary>
    Closed,

    /// <summary>INIT sent, waiting for INIT ACK.</summary>
    CookieWait,

    /// <summary>COOKIE ECHO sent, waiting for COOKIE ACK.</summary>
    CookieEchoed,

    /// <summary>Messages flow both ways.</summary>
    Established,

    /// <summary>A shutdown was asked for: what is queued still goes out, nothing new is taken.</summary>
    ShutdownPending,

    ShutdownSent,

    /// <summary>The peer asked to shut down: what is queued still goes out, nothing new is taken.</summary>
    ShutdownReceived,

    ShutdownAckSent,
}
