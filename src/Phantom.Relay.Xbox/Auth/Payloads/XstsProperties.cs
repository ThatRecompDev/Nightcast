// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The sandbox and user tokens of <see cref="XstsRequest"/>.</summary>
public sealed record XstsProperties(
    [property: JsonPropertyName("SandboxId")] string SandboxId,
    [property: JsonPropertyName("UserTokens")] IReadOnlyList<string> UserTokens);
