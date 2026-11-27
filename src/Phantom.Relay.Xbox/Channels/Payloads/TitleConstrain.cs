// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>Whether the console keeps the running game constrained (false lets it take input).</summary>
public sealed record TitleConstrain(
    [property: JsonPropertyName("constrain")] bool Constrain);
