// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The new session: its id and the path its later calls go to.</summary>
public sealed record PlayReply(
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("sessionPath")] string? SessionPath);
