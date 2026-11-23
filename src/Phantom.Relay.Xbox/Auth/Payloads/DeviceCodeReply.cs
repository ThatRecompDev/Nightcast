// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The Microsoft account service's device code: the short code for the player, the one the app asks with.</summary>
public sealed record DeviceCodeReply(
    [property: JsonPropertyName("user_code")] string? UserCode,
    [property: JsonPropertyName("device_code")] string? DeviceCode,
    [property: JsonPropertyName("verification_uri")] string? VerificationUri,
    [property: JsonPropertyName("interval")] int Interval,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
