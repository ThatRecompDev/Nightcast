// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>Xbox Live user authentication with a Microsoft account ticket.</summary>
public sealed record UserTokenRequest(
    [property: JsonPropertyName("RelyingParty")] string RelyingParty,
    [property: JsonPropertyName("TokenType")] string TokenType,
    [property: JsonPropertyName("Properties")] UserTokenProperties Properties);
