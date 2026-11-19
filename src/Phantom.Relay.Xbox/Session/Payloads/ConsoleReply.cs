// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>One console of <see cref="ConsoleListReply"/>.</summary>
public sealed record ConsoleReply(
    [property: JsonPropertyName("serverId")] string? ServerId,
    [property: JsonPropertyName("deviceName")] string? DeviceName,
    [property: JsonPropertyName("consoleType")] string? ConsoleType,
    [property: JsonPropertyName("powerState")] string? PowerState,
    [property: JsonPropertyName("isStreamingEnabled")] bool? IsStreamingEnabled);
