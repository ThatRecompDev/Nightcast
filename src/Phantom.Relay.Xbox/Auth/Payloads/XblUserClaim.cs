// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>One user's claim: the user hash.</summary>
public sealed record XblUserClaim(
    [property: JsonPropertyName("uhs")] string? UserHash);
