// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>Where an ICE agent is.</summary>
public enum IceAgentState
{
    /// <summary>Made, not checking yet: waiting for the remote credentials and candidates.</summary>
    New,

    Checking,

    /// <summary>A pair succeeded and is nominated: <see cref="IceAgent.Selected"/> is where datagrams go.</summary>
    Connected,

    /// <summary>Every pair failed; the session cannot connect.</summary>
    Failed,
}
