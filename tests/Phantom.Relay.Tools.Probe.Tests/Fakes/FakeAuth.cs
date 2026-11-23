// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Auth;

namespace Phantom.Relay.Tools.Probe.Tests.Fakes;

/// <summary>An account that is signed in once a redirect address is handed to it, or its sign-in code was waited for.</summary>
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

    /// <summary>The sign-in by code handed out, and whether it was waited for.</summary>
    public DeviceSignIn? CodeShown { get; private set; }

    public Task<DeviceSignIn> StartDeviceSignInAsync(CancellationToken cancellation)
    {
        CodeShown = new DeviceSignIn("ABCD1234", new Uri("https://link.example.test/"), TimeSpan.FromSeconds(5), DateTimeOffset.MaxValue) { DeviceCode = "device-code" };

        return Task.FromResult(CodeShown);
    }

    public Task CompleteDeviceSignInAsync(DeviceSignIn signIn, CancellationToken cancellation)
    {
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
