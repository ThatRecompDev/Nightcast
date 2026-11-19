// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Tests.Fakes;

/// <summary>
/// A session service whose answers the test chooses: a refused start, an answer, or a wait for readiness that lasts
/// until the stream is stopped. It records the offer and counts the stops.
/// </summary>
public sealed class FakeSessions : IXHomeSessionService, IDisposable
{
    private static readonly StreamSession Session = new("S1", "v5/sessions/home/S1", new Uri("https://region.example.test/"));

    private int _stops;

    public string Answer { get; set; } = "v=0\r\nm=video 9 UDP/TLS/RTP/SAVPF 104\r\na=rtpmap:104 H264/90000\r\n";

    public bool RefuseStart { get; set; }

    public bool WaitUntilStopped { get; set; }

    public string? StartedOn { get; private set; }

    public string? Offer { get; private set; }

    public IReadOnlyList<IceCandidate>? SentCandidates { get; private set; }

    public int Stops => Volatile.Read(ref _stops);

    /// <summary>Set once the stream waits for readiness, so a test stops it at that point and not before.</summary>
    public ManualResetEventSlim Waiting { get; } = new();

    public Task<IReadOnlyList<XboxConsole>> GetConsolesAsync(CancellationToken cancellation)
    {
        return Task.FromResult<IReadOnlyList<XboxConsole>>([]);
    }

    public Task<StreamSession> StartAsync(string serverId, StreamSettings settings, CancellationToken cancellation)
    {
        StartedOn = serverId;

        if (RefuseStart)
        {
            throw new XHomeSessionException("The service refused the session (status 'failure').");
        }

        return Task.FromResult(Session);
    }

    public async Task WaitUntilReadyAsync(StreamSession session, IProgress<SessionState>? progress, CancellationToken cancellation)
    {
        Waiting.Set();

        if (WaitUntilStopped)
        {
            await Task.Delay(Timeout.Infinite, cancellation);
        }
    }

    public Task<string> ExchangeSdpAsync(StreamSession session, string offer, CancellationToken cancellation)
    {
        Offer = offer;

        return Task.FromResult(Answer);
    }

    public Task<IReadOnlyList<IceCandidate>> ExchangeIceAsync(StreamSession session, IReadOnlyList<IceCandidate> local, CancellationToken cancellation)
    {
        SentCandidates = local;

        return Task.FromResult<IReadOnlyList<IceCandidate>>([]);
    }

    public Task KeepAliveAsync(StreamSession session, CancellationToken cancellation)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Waiting.Dispose();
    }

    public Task StopAsync(StreamSession session, CancellationToken cancellation)
    {
        Interlocked.Increment(ref _stops);

        return Task.CompletedTask;
    }
}
