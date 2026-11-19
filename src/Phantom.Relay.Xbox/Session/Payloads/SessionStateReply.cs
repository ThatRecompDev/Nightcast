// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>Where a session's provisioning is, and why it failed when it did.</summary>
public sealed record SessionStateReply(
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("errorDetails")] SessionErrorReply? ErrorDetails);
