// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The client's SDP offer with the channel versions it speaks.</summary>
public sealed record SdpOfferRequest(
    [property: JsonPropertyName("messageType")] string MessageType,
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("configuration")] SdpConfiguration Configuration);
