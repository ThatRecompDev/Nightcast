// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The user claims of a token; the first entry carries the user hash.</summary>
public sealed record XblDisplayClaims(
    [property: JsonPropertyName("xui")] IReadOnlyList<XblUserClaim>? Users);
