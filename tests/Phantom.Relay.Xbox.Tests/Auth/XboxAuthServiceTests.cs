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
    private readonly List<TimeSpan> _waits = [];

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

    [Fact]
    public async Task A_sign_in_code_is_asked_after_until_it_was_entered_and_then_keeps_the_account()
    {
        _http.DeviceCode().Pending().Pending().MsaGrant("access-1", 3600).TokenChain();
        using var auth = Service();

        var signIn = await auth.StartDeviceSignInAsync(CancellationToken.None);
        await auth.CompleteDeviceSignInAsync(signIn, CancellationToken.None);
        var token = await auth.GetStreamingTokenAsync(CancellationToken.None);

        Assert.Equal("ABCD1234", signIn.UserCode);
        Assert.Equal(new Uri("https://www.microsoft.com/link"), signIn.Page);
        Assert.DoesNotContain("device-secret", signIn.ToString(), StringComparison.Ordinal);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)], _waits);

        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", _http.BodyOf(1), StringComparison.Ordinal);
        Assert.Contains("device_code=device-secret", _http.BodyOf(1), StringComparison.Ordinal);
        Assert.Equal("gs-token", token.Value);
        Assert.NotNull(new CredentialStore(_store).Load());
    }

    [Fact]
    public async Task A_slow_down_waits_longer_and_a_refusal_ends_the_sign_in()
    {
        _http.DeviceCode()
            .Answer(HttpVerb.Post, XboxEndpoints.MsaToken, 400, """{"error":"slow_down"}""")
            .Answer(HttpVerb.Post, XboxEndpoints.MsaToken, 400, """{"error":"access_denied","error_description":"The user declined."}""");
        using var auth = Service();
        var signIn = await auth.StartDeviceSignInAsync(CancellationToken.None);

        var error = await Assert.ThrowsAsync<XboxAuthException>(() => auth.CompleteDeviceSignInAsync(signIn, CancellationToken.None));

        Assert.Contains("The user declined.", error.Message, StringComparison.Ordinal);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], _waits);
        Assert.False(auth.IsSignedIn);
    }

    [Fact]
    public async Task A_code_never_entered_expires()
    {
        _http.DeviceCode(expiresIn: 10).Pending().Pending();
        using var auth = Service();
        var signIn = await auth.StartDeviceSignInAsync(CancellationToken.None);

        var error = await Assert.ThrowsAsync<XboxAuthException>(() => auth.CompleteDeviceSignInAsync(signIn, CancellationToken.None));

        Assert.Contains("expired", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refresh_token_no_longer_accepted_is_forgotten_and_asks_for_a_new_sign_in()
    {
        _http.MsaGrant("access-1", 600).TokenChain()
            .Answer(HttpVerb.Post, XboxEndpoints.MsaToken, 400, """{"error":"invalid_grant","error_description":"The token has expired."}""");
        using var auth = Service();
        await auth.CompleteSignInAsync(Redirected, CancellationToken.None);
        await auth.GetStreamingTokenAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromHours(2));
        var error = await Assert.ThrowsAsync<XboxAuthException>(() => auth.GetStreamingTokenAsync(CancellationToken.None));

        Assert.True(error.SignInAgain);
        Assert.False(auth.IsSignedIn);
        Assert.Null(new CredentialStore(_store).Load());
    }

    private XboxAuthService Service()
    {
        return new XboxAuthService(_http, new CredentialStore(_store), _clock, NullLogger<XboxAuthService>.Instance, Wait);
    }

    /// <summary>Each wait is recorded and passes on the manual clock at once.</summary>
    private Task Wait(TimeSpan delay, CancellationToken cancellation)
    {
        _waits.Add(delay);
        _clock.Advance(delay);

        return Task.CompletedTask;
    }
}
