// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Setup;

/// <summary>
/// Everything before the stream: signing in and choosing the console (<see cref="TextSetup"/>), its lines written as it
/// goes to wherever the head shows them (the text console, the window's setup screen).
/// </summary>
public interface ISetupFlow
{
    /// <summary>
    /// Signs in and picks the console; null when there is none, the choice was not one, or the account or the service
    /// refused (the reason written as a line). Any other failure (the network) is thrown.
    /// </summary>
    Task<XboxConsole?> RunAsync(CancellationToken cancellation);
}
