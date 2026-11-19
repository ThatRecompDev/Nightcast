// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The codec and container of party-chat sound.</summary>
public sealed record ChatFormat(
    [property: JsonPropertyName("codec")] string Codec,
    [property: JsonPropertyName("container")] string Container);
