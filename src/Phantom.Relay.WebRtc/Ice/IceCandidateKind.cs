// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>Where a candidate's address comes from (RFC 8445 section 5.1.1), by its SDP "typ".</summary>
public enum IceCandidateKind
{
    Host,
    ServerReflexive,
    PeerReflexive,
    Relayed,
}
