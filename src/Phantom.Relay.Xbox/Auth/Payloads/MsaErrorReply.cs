// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The Microsoft account token endpoint's refusal (an OAuth error code and its description).</summary>
public sealed record MsaErrorReply(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? Description);
