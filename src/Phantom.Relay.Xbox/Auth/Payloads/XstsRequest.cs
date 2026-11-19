// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>XSTS authorization of a user token for one relying party.</summary>
public sealed record XstsRequest(
    [property: JsonPropertyName("RelyingParty")] string RelyingParty,
    [property: JsonPropertyName("TokenType")] string TokenType,
    [property: JsonPropertyName("Properties")] XstsProperties Properties);
