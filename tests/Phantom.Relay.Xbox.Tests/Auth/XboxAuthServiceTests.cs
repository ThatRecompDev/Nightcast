// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Http;
using Phantom.Relay.Xbox.Tests.Fakes;

namespace Phantom.Relay.Xbox.Tests.Auth;

public sealed class XboxAuthServiceTests
{
    private static readonly Uri Redirected = new("https://login.live.com/oauth20_desktop.srf?code=the-code");

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly MemoryPersistentStore _store = new();
    private readonly ScriptedHttpExchange _http = new();

    [Fact]
    public async Task Signing_in_keeps_the_account_tokens_and_walks_the_chain_to_a_streaming_token()
    {
        _http.MsaGrant("access-1", 3600).TokenChain();
        using var auth = Service();

        await auth.CompleteSignInAsync(Redirected, CancellationToken.None);
        var token = await auth.GetStreamingTokenAsync(CancellationToken.None);

        Assert.Equal("gs-token", token.Value);
        Assert.Equal("Home", token.DefaultRegion.Name);
        Assert.True(auth.IsSignedIn);
        Assert.NotNull(new CredentialStore(_store).Load());

        Assert.Contains("grant_type=authorization_code", _http.BodyOf(0), StringComparison.Ordinal);
        Assert.Contains("\"RpsTicket\":\"t=access-1\"", _http.BodyOf(1), StringComparison.Ordinal);
        Assert.Contains("\"RelyingParty\":\"http://gssv.xboxlive.com/\"", _http.BodyOf(2), StringComparison.Ordinal);
        Assert.Contains("\"offeringId\":\"xhome\"", _http.BodyOf(3), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_valid_streaming_token_is_reused_without_a_request()
    {
        _http.MsaGrant("access-1", 3600).TokenChain();
        using var auth = Service();
        await auth.CompleteSignInAsync(Redirected, CancellationToken.None);

        var first = await auth.GetStreamingTokenAsync(CancellationToken.None);
        var second = await auth.GetStreamingTokenAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(4, _http.Requests.Count);
    }

    [Fact]
    public async Task An_expired_account_token_is_renewed_from_the_kept_refresh_token()
    {
        _http.MsaGrant("access-1", 600).TokenChain().MsaGrant("access-2", 3600).StreamingLogin();
        using var auth = Service();
        await auth.CompleteSignInAsync(Redirected, CancellationToken.None);
        await auth.GetStreamingTokenAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromHours(2));
        await auth.GetStreamingTokenAsync(CancellationToken.None);

        Assert.Contains("grant_type=refresh_token", _http.BodyOf(4), StringComparison.Ordinal);
        Assert.Contains("refresh_token=refresh-1", _http.BodyOf(4), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_child_account_is_refused_in_the_players_words()
    {
        _http.MsaGrant("access-1", 3600)
            .Answer(HttpVerb.Post, XboxEndpoints.UserAuthenticate, 200, """{"Token":"user-token","NotAfter":"2026-12-01T00:00:00Z"}""")
            .Answer(HttpVerb.Post, XboxEndpoints.XstsAuthorize, 401, """{"XErr":2148916238}""");
        using var auth = Service();
        await auth.CompleteSignInAsync(Redirected, CancellationToken.None);

        var error = await Assert.ThrowsAsync<XboxAuthException>(() => auth.GetStreamingTokenAsync(CancellationToken.None));

        Assert.Contains("child account", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_sign_in_a_token_is_refused()
    {
        using var auth = Service();

        await Assert.ThrowsAsync<XboxAuthException>(() => auth.GetStreamingTokenAsync(CancellationToken.None));
        Assert.False(auth.IsSignedIn);
    }

    [Fact]
    public async Task Signing_out_forgets_the_kept_tokens()
    {
        _http.MsaGrant("access-1", 3600);
        using var auth = Service();
        await auth.CompleteSignInAsync(Redirected, CancellationToken.None);

        auth.SignOut();

        Assert.False(auth.IsSignedIn);
        Assert.Null(new CredentialStore(_store).Load());
    }

    private XboxAuthService Service()
    {
        return new XboxAuthService(_http, new CredentialStore(_store), _clock, NullLogger<XboxAuthService>.Instance);
    }
}
