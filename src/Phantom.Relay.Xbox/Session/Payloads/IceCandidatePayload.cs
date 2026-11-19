// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>One ICE candidate as both sides write it.</summary>
public sealed record IceCandidatePayload(
    [property: JsonPropertyName("candidate")] string? Candidate,
    [property: JsonPropertyName("sdpMid")] string? SdpMid,
    [property: JsonPropertyName("sdpMLineIndex")] int SdpMLineIndex);
