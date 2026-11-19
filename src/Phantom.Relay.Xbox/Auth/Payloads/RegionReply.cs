// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>One region of an offering: its name, the base of its REST calls and whether it is the default.</summary>
public sealed record RegionReply(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("baseUri")] string? BaseUri,
    [property: JsonPropertyName("isDefault")] bool IsDefault);
