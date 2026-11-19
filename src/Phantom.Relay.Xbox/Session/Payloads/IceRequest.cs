// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The client's ICE candidates.</summary>
public sealed record IceRequest(
    [property: JsonPropertyName("messageType")] string MessageType,
    [property: JsonPropertyName("candidate")] IReadOnlyList<IceCandidatePayload> Candidates);
