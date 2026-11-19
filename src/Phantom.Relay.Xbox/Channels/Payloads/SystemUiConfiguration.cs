// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>Which of the console's system screens the client draws itself (none).</summary>
public sealed record SystemUiConfiguration(
    [property: JsonPropertyName("version")] IReadOnlyList<int> Version,
    [property: JsonPropertyName("systemUis")] IReadOnlyList<int> SystemUis);
