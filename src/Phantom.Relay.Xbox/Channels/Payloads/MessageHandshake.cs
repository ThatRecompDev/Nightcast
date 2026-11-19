// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>The message channel's handshake.</summary>
public sealed record MessageHandshake(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("cv")] string CorrelationVector);
