// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>A user or XSTS token with its expiry and the user's claims.</summary>
public sealed record XblTokenReply(
    [property: JsonPropertyName("Token")] string? Token,
    [property: JsonPropertyName("NotAfter")] DateTimeOffset NotAfter,
    [property: JsonPropertyName("DisplayClaims")] XblDisplayClaims? DisplayClaims);
