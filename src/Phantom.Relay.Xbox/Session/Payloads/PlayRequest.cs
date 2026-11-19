// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>Starts a remote-play session on one console.</summary>
public sealed record PlayRequest(
    [property: JsonPropertyName("clientSessionId")] string ClientSessionId,
    [property: JsonPropertyName("titleId")] string TitleId,
    [property: JsonPropertyName("systemUpdateGroup")] string SystemUpdateGroup,
    [property: JsonPropertyName("serverId")] string ServerId,
    [property: JsonPropertyName("fallbackRegionNames")] IReadOnlyList<string> FallbackRegionNames,
    [property: JsonPropertyName("settings")] PlaySettings Settings);
