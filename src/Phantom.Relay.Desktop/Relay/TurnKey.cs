// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Relay;

/// <summary>
/// A Cloudflare TURN key, as the user's turn.json holds it (the same shape as the devices gateway's): the key's id and
/// an API token allowed to make credentials with it. Never in the repository or a release.
/// </summary>
internal sealed record TurnKey(string? KeyId, string? ApiToken);
