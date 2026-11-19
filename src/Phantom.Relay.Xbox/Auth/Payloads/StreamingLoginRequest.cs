// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The streaming service's login with an XSTS token for one offering.</summary>
public sealed record StreamingLoginRequest(
    [property: JsonPropertyName("offeringId")] string OfferingId,
    [property: JsonPropertyName("token")] string Token);
