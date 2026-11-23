// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Desktop.Setup;

/// <summary>A setup that cannot begin (no network, no decoder): it writes why, and ends without a console.</summary>
internal sealed class RefusedSetup : ISetupFlow
{
    private readonly string _reason;
    private readonly TextWriter _output;

    public RefusedSetup(string reason, TextWriter output)
    {
        _reason = reason;
        _output = output;
    }

    public Task<XboxConsole?> RunAsync(CancellationToken cancellation)
    {
        _output.WriteLine(_reason);

        return Task.FromResult<XboxConsole?>(null);
    }
}
