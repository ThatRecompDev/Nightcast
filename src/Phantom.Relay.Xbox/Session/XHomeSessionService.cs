// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Http;
using Phantom.Relay.Xbox.Json;
using Phantom.Relay.Xbox.Logging;
using Phantom.Relay.Xbox.Session.Payloads;

namespace Phantom.Relay.Xbox.Session;

/// <summary>
/// The console offering's REST calls (docs/architecture/xhome.md#sessions): the console list, play, the state poll,
/// the SDP and ICE exchanges (each a POST, then GETs until the console has answered), keep-alive and stop.
/// </summary>
public sealed class XHomeSessionService : IXHomeSessionService
{
    private readonly IHttpExchange _http;
    private readonly IXboxAuthService _auth;
    private readonly TimeProvider _clock;
    private readonly ILogger<XHomeSessionService> _log;

    public XHomeSessionService(IHttpExchange http, IXboxAuthService auth, TimeProvider clock, ILogger<XHomeSessionService> log)
    {
        _http = http;
        _auth = auth;
        _clock = clock;
        _log = log;
    }

    public async Task<IReadOnlyList<XboxConsole>> GetConsolesAsync(CancellationToken cancellation)
    {
        var token = await _auth.GetStreamingTokenAsync(cancellation);
        var address = new Uri(token.DefaultRegion.BaseUri, XHomeProtocol.ConsolesPath);
        var reply = await SendAsync(HttpVerb.Get, address, cancellation);
        var list = Require(HttpRequests.Read(reply, RelayJsonContext.Default.ConsoleListReply), "console list");

        return XboxConsoles.From(list.Results);
    }

    public async Task<StreamSession> StartAsync(string serverId, StreamSettings settings, CancellationToken cancellation)
    {
        var token = await _auth.GetStreamingTokenAsync(cancellation);
        var baseUri = token.DefaultRegion.BaseUri;
        var body = PlayRequests.For(serverId, settings);

        var reply = await SendJsonAsync(new Uri(baseUri, XHomeProtocol.PlayPath), body, RelayJsonContext.Default.PlayRequest, cancellation);
        var play = Require(HttpRequests.Read(reply, RelayJsonContext.Default.PlayReply), "play");

        if (play.SessionPath is null)
        {
            throw new XHomeSessionException("The console's play answer has no session path.");
        }

        string path = play.SessionPath.TrimStart('/');
        _log.SessionStarted(path);

        return new StreamSession(play.SessionId ?? path, path, baseUri);
    }

    public async Task WaitUntilReadyAsync(StreamSession session, IProgress<SessionState>? progress, CancellationToken cancellation)
    {
        using var timeout = new CancellationTokenSource(XHomeProtocol.ProvisionTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);

        while (true)
        {
            var reply = await SendAsync(HttpVerb.Get, session.Address(XHomeProtocol.StatePath), linked.Token);
            var answer = HttpRequests.Read(reply, RelayJsonContext.Default.SessionStateReply);
            var state = SessionStates.Parse(answer?.State);
            progress?.Report(state);

            if (SessionStates.IsReady(state))
            {
                return;
            }

            if (state == SessionState.Failed)
            {
                string reason = answer?.ErrorDetails?.Message ?? answer?.ErrorDetails?.Code ?? "no reason given";

                throw new XHomeSessionException($"The console could not start the session: {reason}");
            }

            await Task.Delay(XHomeProtocol.StatePollInterval, _clock, linked.Token);
        }
    }

    public async Task<string> ExchangeSdpAsync(StreamSession session, string offer, CancellationToken cancellation)
    {
        var body = new SdpOfferRequest(XHomeProtocol.OfferMessage, offer, XHomeProtocol.FirstRequestId, SdpConfigurations.Client());
        await SendJsonAsync(session.Address(XHomeProtocol.SdpPath), body, RelayJsonContext.Default.SdpOfferRequest, cancellation);

        string exchange = await PollExchangeAsync(session.Address(XHomeProtocol.SdpPath), cancellation);
        var answer = Require(JsonSerializer.Deserialize(exchange, RelayJsonContext.Default.SdpAnswer), "SDP answer");

        if (answer.Status != XHomeProtocol.AnswerAccepted || answer.Sdp is null)
        {
            throw new XHomeSessionException($"The console refused the offer (status '{answer.Status}').");
        }

        return answer.Sdp;
    }

    public async Task<IReadOnlyList<IceCandidate>> ExchangeIceAsync(StreamSession session, IReadOnlyList<IceCandidate> local, CancellationToken cancellation)
    {
        var body = new IceRequest(XHomeProtocol.IceMessage, IceCandidates.ToPayloads(local));
        await SendJsonAsync(session.Address(XHomeProtocol.IcePath), body, RelayJsonContext.Default.IceRequest, cancellation);

        string exchange = await PollExchangeAsync(session.Address(XHomeProtocol.IcePath), cancellation);
        var remote = JsonSerializer.Deserialize(exchange, RelayJsonContext.Default.IReadOnlyListIceCandidatePayload);

        return IceCandidates.FromPayloads(remote);
    }

    public async Task KeepAliveAsync(StreamSession session, CancellationToken cancellation)
    {
        await SendAsync(HttpVerb.Post, session.Address(XHomeProtocol.KeepAlivePath), cancellation);
    }

    public async Task StopAsync(StreamSession session, CancellationToken cancellation)
    {
        await SendAsync(HttpVerb.Delete, session.Root, cancellation);
    }

    private static T Require<T>(T? value, string what)
        where T : class
    {
        if (value is null)
        {
            throw new XHomeSessionException($"The {what} answer was empty.");
        }

        return value;
    }

    /// <summary>GETs <paramref name="address"/> until the console's side of the exchange is there.</summary>
    private async Task<string> PollExchangeAsync(Uri address, CancellationToken cancellation)
    {
        using var timeout = new CancellationTokenSource(XHomeProtocol.ExchangeTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);

        while (true)
        {
            var reply = await SendAsync(HttpVerb.Get, address, linked.Token);
            string? exchange = HttpRequests.Read(reply, RelayJsonContext.Default.ExchangeReply)?.ExchangeResponse;

            if (exchange is not null)
            {
                return exchange;
            }

            await Task.Delay(XHomeProtocol.ExchangePollInterval, _clock, linked.Token);
        }
    }

    private async Task<HttpReply> SendAsync(HttpVerb verb, Uri address, CancellationToken cancellation)
    {
        var headers = await HeadersAsync(cancellation);

        return await CheckedAsync(HttpRequests.Empty(verb, address, headers), cancellation);
    }

    private async Task<HttpReply> SendJsonAsync<T>(Uri address, T body, JsonTypeInfo<T> type, CancellationToken cancellation)
    {
        var headers = await HeadersAsync(cancellation);

        return await CheckedAsync(HttpRequests.Json(HttpVerb.Post, address, headers, body, type), cancellation);
    }

    private async Task<HttpReply> CheckedAsync(HttpRequest request, CancellationToken cancellation)
    {
        var reply = await _http.SendAsync(request, cancellation);

        if (!reply.IsSuccess)
        {
            string path = request.Address.AbsolutePath;
            _log.SessionRequestFailed(request.Verb, path, reply.Status);

            throw new XHomeSessionException($"The streaming service refused {path} (HTTP {reply.Status}).");
        }

        return reply;
    }

    private async Task<HttpHeader[]> HeadersAsync(CancellationToken cancellation)
    {
        var token = await _auth.GetStreamingTokenAsync(cancellation);

        return
        [
            new HttpHeader(XboxEndpoints.AuthorizationHeader, XboxEndpoints.BearerPrefix + token.Value),
            new HttpHeader(XboxEndpoints.StreamingClientHeader, XboxEndpoints.StreamingClient),
            new HttpHeader(XboxEndpoints.DeviceInfoHeader, DeviceInfo.Json),
        ];
    }
}
