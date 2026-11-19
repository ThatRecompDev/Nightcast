// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream;

/// <summary>Where a <see cref="StreamConnection"/> is, from its start to its end.</summary>
public enum StreamState
{
    /// <summary>Made, not started.</summary>
    Idle,

    /// <summary>The session is asked for and provisioned on the console.</summary>
    Starting,

    /// <summary>The offer and the candidates go to the console and its answer comes back.</summary>
    Negotiating,

    /// <summary>ICE finds the path, then the DTLS handshake runs over it.</summary>
    Connecting,

    /// <summary>The data channels run and the media arrives.</summary>
    Streaming,

    /// <summary>The session is being stopped on the console.</summary>
    Stopping,

    /// <summary>Ended cleanly, by <see cref="StreamConnection.Stop"/>.</summary>
    Stopped,

    /// <summary>Ended by an error; <see cref="StreamStats.FailureReason"/> says which.</summary>
    Failed,
}
