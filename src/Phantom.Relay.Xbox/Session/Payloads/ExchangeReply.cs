// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The console's side of an SDP or ICE exchange, itself a JSON document in a string.</summary>
public sealed record ExchangeReply(
    [property: JsonPropertyName("exchangeResponse")] string? ExchangeResponse);
