// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Tests.Setup;

/// <summary>A setup whose network is down.</summary>
public sealed class FailingSetup : ISetupFlow
{
    public Task<XboxConsole?> RunAsync(CancellationToken cancellation)
    {
        return Task.FromException<XboxConsole?>(new HttpRequestException("no route to the service"));
    }
}
