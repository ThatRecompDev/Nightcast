// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>Asks the console for a key picture (after loss, or when the decoder starts).</summary>
public sealed record ControlKeyframeRequest(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("ifrRequested")] bool IfrRequested);
