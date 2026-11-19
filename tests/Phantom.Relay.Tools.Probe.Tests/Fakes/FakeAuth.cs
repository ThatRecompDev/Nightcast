// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Auth;

namespace Phantom.Relay.Tools.Probe.Tests.Fakes;

/// <summary>An account that is signed in once a redirect address is handed to it.</summary>
public sealed class FakeAuth : IXboxAuthService
{
    private static readonly StreamingToken Token = new(
        "gs-token",
        DateTimeOffset.MaxValue,
        [new StreamingRegion("Home", new Uri("https://region.example.test/"), true)]);

    public bool IsSignedIn { get; set; }

    public Uri? Redirected { get; private set; }

    public Uri SignInAddress => new("https://login.example.test/authorize");

    public Task CompleteSignInAsync(Uri redirected, CancellationToken cancellation)
    {
        Redirected = redirected;
        IsSignedIn = true;

        return Task.CompletedTask;
    }

    public Task<StreamingToken> GetStreamingTokenAsync(CancellationToken cancellation)
    {
        return Task.FromResult(Token);
    }

    public void SignOut()
    {
        IsSignedIn = false;
    }
}
