// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>A message channel message: its kind, its target path and its content, itself JSON in a string.</summary>
public sealed record MessageEnvelope(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("target")] string? Target,
    [property: JsonPropertyName("cv")] string? CorrelationVector);
