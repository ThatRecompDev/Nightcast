// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The streaming token, how long it lasts and the offering's regions.</summary>
public sealed record StreamingLoginReply(
    [property: JsonPropertyName("gsToken")] string? StreamingToken,
    [property: JsonPropertyName("durationInSeconds")] int DurationInSeconds,
    [property: JsonPropertyName("offeringSettings")] OfferingSettings? OfferingSettings);
