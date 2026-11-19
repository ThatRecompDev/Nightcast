// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The console's SDP answer, read from <see cref="ExchangeReply.ExchangeResponse"/>.</summary>
public sealed record SdpAnswer(
    [property: JsonPropertyName("sdp")] string? Sdp,
    [property: JsonPropertyName("sdpType")] string? SdpType,
    [property: JsonPropertyName("status")] string? Status);
