// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>A console session's life and its WebRTC signaling over the streaming service's REST calls.</summary>
public interface IXHomeSessionService
{
    Task<IReadOnlyList<XboxConsole>> GetConsolesAsync(CancellationToken cancellation);

    Task<StreamSession> StartAsync(string serverId, StreamSettings settings, CancellationToken cancellation);

    /// <summary>Asks for the state until the session takes an offer; reports each state it sees.</summary>
    Task WaitUntilReadyAsync(StreamSession session, IProgress<SessionState>? progress, CancellationToken cancellation);

    /// <summary>Sends the client's SDP offer and returns the console's answer.</summary>
    Task<string> ExchangeSdpAsync(StreamSession session, string offer, CancellationToken cancellation);

    /// <summary>Sends the client's ICE candidates and returns the console's.</summary>
    Task<IReadOnlyList<IceCandidate>> ExchangeIceAsync(StreamSession session, IReadOnlyList<IceCandidate> local, CancellationToken cancellation);

    Task KeepAliveAsync(StreamSession session, CancellationToken cancellation);

    Task StopAsync(StreamSession session, CancellationToken cancellation);
}
