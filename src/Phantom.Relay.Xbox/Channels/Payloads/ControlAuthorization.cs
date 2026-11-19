// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>The control channel's first message.</summary>
public sealed record ControlAuthorization(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("accessKey")] string AccessKey);
