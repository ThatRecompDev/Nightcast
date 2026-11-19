// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe.Tests.Fakes;

/// <summary>Consoles and a session that is ready at once; the answer (or a refusal) is chosen by the test.</summary>
public sealed class FakeSessions : IXHomeSessionService
{
    private static readonly StreamSession Session = new("S1", "v5/sessions/home/S1", new Uri("https://region.example.test/"));

    public List<XboxConsole> Consoles { get; } = [];

    public string Answer { get; set; } = "v=0\r\nm=video 9 UDP/TLS/RTP/SAVPF 102\r\na=rtpmap:102 H264/90000\r\n";

    public bool RefuseOffer { get; set; }

    public string? StartedOn { get; private set; }

    public StreamSettings? StartedWith { get; private set; }

    public string? Offer { get; private set; }

    public int Stops { get; private set; }

    public Task<IReadOnlyList<XboxConsole>> GetConsolesAsync(CancellationToken cancellation)
    {
        return Task.FromResult<IReadOnlyList<XboxConsole>>(Consoles);
    }

    public Task<StreamSession> StartAsync(string serverId, StreamSettings settings, CancellationToken cancellation)
    {
        StartedOn = serverId;
        StartedWith = settings;

        return Task.FromResult(Session);
    }

    public Task WaitUntilReadyAsync(StreamSession session, IProgress<SessionState>? progress, CancellationToken cancellation)
    {
        progress?.Report(SessionState.Provisioning);
        progress?.Report(SessionState.Provisioned);

        return Task.CompletedTask;
    }

    public Task<string> ExchangeSdpAsync(StreamSession session, string offer, CancellationToken cancellation)
    {
        Offer = offer;

        if (RefuseOffer)
        {
            throw new XHomeSessionException("The console refused the offer (status 'failure').");
        }

        return Task.FromResult(Answer);
    }

    public Task<IReadOnlyList<IceCandidate>> ExchangeIceAsync(StreamSession session, IReadOnlyList<IceCandidate> local, CancellationToken cancellation)
    {
        return Task.FromResult<IReadOnlyList<IceCandidate>>([]);
    }

    public Task KeepAliveAsync(StreamSession session, CancellationToken cancellation)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(StreamSession session, CancellationToken cancellation)
    {
        Stops++;

        return Task.CompletedTask;
    }
}
