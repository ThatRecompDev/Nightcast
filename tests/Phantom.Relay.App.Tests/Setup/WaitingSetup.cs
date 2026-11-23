// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Tests.Setup;

/// <summary>A sign-in that waits until it is cancelled.</summary>
public sealed class WaitingSetup : ISetupFlow
{
    public ManualResetEventSlim Started { get; } = new();

    public ManualResetEventSlim Cancelled { get; } = new();

    public async Task<XboxConsole?> RunAsync(CancellationToken cancellation)
    {
        Started.Set();

        try
        {
            await Task.Delay(Timeout.Infinite, cancellation);
        }
        catch (OperationCanceledException)
        {
            Cancelled.Set();
        }

        return null;
    }
}
