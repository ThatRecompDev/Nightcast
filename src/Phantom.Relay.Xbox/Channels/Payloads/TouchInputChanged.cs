// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>Whether the client sends touch input.</summary>
public sealed record TouchInputChanged(
    [property: JsonPropertyName("touchInputEnabled")] bool TouchInputEnabled);
