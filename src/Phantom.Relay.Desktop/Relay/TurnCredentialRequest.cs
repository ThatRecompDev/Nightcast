// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Relay;

/// <summary>Cloudflare's request for short-lived TURN credentials: how many seconds they last.</summary>
internal sealed record TurnCredentialRequest(int Ttl);
