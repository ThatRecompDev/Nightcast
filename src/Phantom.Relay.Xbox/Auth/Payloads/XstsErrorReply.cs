// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>Why XSTS refused an account (401).</summary>
public sealed record XstsErrorReply(
    [property: JsonPropertyName("XErr")] long Error);
