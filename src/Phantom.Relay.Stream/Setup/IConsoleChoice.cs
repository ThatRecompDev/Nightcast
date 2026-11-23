// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Setup;

/// <summary>
/// How the player picks one of several consoles: typed on a text console (<see cref="TextConsoleChoice"/>) or chosen
/// on the window's setup screen with the pad or the keys. The consoles were already listed to the player.
/// </summary>
public interface IConsoleChoice
{
    /// <summary>The index of the console picked in <paramref name="consoles"/>; null when none was (cancelled, not a number).</summary>
    Task<int?> ChooseAsync(IReadOnlyList<XboxConsole> consoles, CancellationToken cancellation);
}
