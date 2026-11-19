// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Http;
using Phantom.Relay.Xbox.Session;
using Phantom.Relay.Xbox.Tests.Fakes;

namespace Phantom.Relay.Xbox.Tests.Session;

public sealed class XHomeSessionServiceTests : IDisposable
{
    private static readonly StreamSession Session = new("S1", "v5/sessions/home/S1", SignInScript.RegionBase);

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly ScriptedHttpExchange _http = new();
    private readonly XboxAuthService _auth;
    private readonly XHomeSessionService _sessions;

    public XHomeSessionServiceTests()
    {
        var store = new MemoryPersistentStore();
        var credentials = new CredentialStore(store);
        credentials.Save(new StoredCredentials(new MsaToken("access", "refresh", _clock.GetUtcNow().AddHours(1))));
        _http.TokenChain();

        _auth = new XboxAuthService(_http, credentials, _clock, NullLogger<XboxAuthService>.Instance);
        _sessions = new XHomeSessionService(_http, _auth, _clock, NullLogger<XHomeSessionService>.Instance);
    }

    [Fact]
    public async Task The_consoles_are_read_and_entries_without_a_server_id_are_skipped()
    {
        _http.Answer(HttpVerb.Get, new Uri(SignInScript.RegionBase, "v6/servers/home"), 200,
            """{"results":[{"serverId":"F4001","deviceName":"Living room","consoleType":"XboxSeriesX","powerState":"ConnectedStandby","isStreamingEnabled":true},{"deviceName":"broken"}]}""");

        var consoles = await _sessions.GetConsolesAsync(CancellationToken.None);

        var console = Assert.Single(consoles);
        Assert.Equal("F4001", console.ServerId);
        Assert.Equal("Living room", console.Name);
        Assert.Equal("gs-token", BearerOf(_http.Requests[^1]));
    }

    [Fact]
    public async Task Starting_reports_the_tier_through_the_os_name_and_reads_the_session_path()
    {
        _http.Answer(HttpVerb.Post, new Uri(SignInScript.RegionBase, "v5/sessions/home/play"), 200,
            """{"sessionId":"S1","sessionPath":"/v5/sessions/home/S1"}""");

        var session = await _sessions.StartAsync("F4001", new StreamSettings(StreamResolution.P1080High, "en-US", 60), CancellationToken.None);

        Assert.Equal("v5/sessions/home/S1", session.SessionPath);
        Assert.Equal(new Uri("https://region.example.test/v5/sessions/home/S1/sdp"), session.Address("sdp"));
        Assert.Contains("\"osName\":\"tizen\"", _http.BodyOf(_http.Requests.Count - 1), StringComparison.Ordinal);
        Assert.Contains("\"serverId\":\"F4001\"", _http.BodyOf(_http.Requests.Count - 1), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_provisioned_session_is_ready_and_a_failed_one_throws_its_reason()
    {
        var progress = new List<SessionState>();
        _http.Answer(HttpVerb.Get, Session.Address("state"), 200, """{"state":"Provisioned"}""");
        _http.Answer(HttpVerb.Get, Session.Address("state"), 200, """{"state":"Failed","errorDetails":{"code":"X","message":"Console is off"}}""");

        await _sessions.WaitUntilReadyAsync(Session, new SynchronousProgress(progress), CancellationToken.None);
        var error = await Assert.ThrowsAsync<XHomeSessionException>(() => _sessions.WaitUntilReadyAsync(Session, null, CancellationToken.None));

        Assert.Equal([SessionState.Provisioned], progress);
        Assert.Contains("Console is off", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sdp_exchange_posts_the_offer_and_reads_the_answer_from_its_string()
    {
        _http.Answer(HttpVerb.Post, Session.Address("sdp"), 202, string.Empty);
        _http.Answer(HttpVerb.Get, Session.Address("sdp"), 200,
            """{"exchangeResponse":"{\"sdp\":\"v=0 answer\",\"sdpType\":\"answer\",\"status\":\"success\"}"}""");

        string answer = await _sessions.ExchangeSdpAsync(Session, "v=0 offer", CancellationToken.None);

        Assert.Equal("v=0 answer", answer);
        string offer = _http.BodyOf(_http.Requests.Count - 2);
        Assert.Contains("\"messageType\":\"offer\"", offer, StringComparison.Ordinal);
        Assert.Contains("\"input\":{\"minVersion\":1,\"maxVersion\":8}", offer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_offer_throws()
    {
        _http.Answer(HttpVerb.Post, Session.Address("sdp"), 202, string.Empty);
        _http.Answer(HttpVerb.Get, Session.Address("sdp"), 200, """{"exchangeResponse":"{\"status\":\"failure\"}"}""");

        await Assert.ThrowsAsync<XHomeSessionException>(() => _sessions.ExchangeSdpAsync(Session, "v=0", CancellationToken.None));
    }

    [Fact]
    public async Task The_ice_exchange_sends_an_end_marker_and_returns_the_consoles_candidates_bare()
    {
        _http.Answer(HttpVerb.Post, Session.Address("ice"), 202, string.Empty);
        _http.Answer(HttpVerb.Get, Session.Address("ice"), 200,
            """{"exchangeResponse":"[{\"candidate\":\"a=candidate:1 1 UDP 100 10.0.0.2 9002 typ host \",\"sdpMid\":\"0\",\"sdpMLineIndex\":0},{\"candidate\":\"a=end-of-candidates\",\"sdpMid\":\"0\",\"sdpMLineIndex\":0}]"}""");
        IceCandidate[] local = [new("candidate:7 1 udp 2130706431 192.168.1.5 50000 typ host", "0", 0)];

        var remote = await _sessions.ExchangeIceAsync(Session, local, CancellationToken.None);

        var candidate = Assert.Single(remote);
        Assert.StartsWith("candidate:1 1 UDP", candidate.Candidate, StringComparison.Ordinal);
        Assert.Contains("a=end-of-candidates", _http.BodyOf(_http.Requests.Count - 2), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_call_throws_with_its_status()
    {
        _http.Answer(HttpVerb.Post, Session.Address("keepalive"), 404, string.Empty);

        var error = await Assert.ThrowsAsync<XHomeSessionException>(() => _sessions.KeepAliveAsync(Session, CancellationToken.None));

        Assert.Contains("404", error.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _auth.Dispose();
    }

    private static string BearerOf(HttpRequest request)
    {
        foreach (var header in request.Headers)
        {
            if (header.Name == XboxEndpoints.AuthorizationHeader)
            {
                return header.Value[XboxEndpoints.BearerPrefix.Length..];
            }
        }

        return string.Empty;
    }
}
