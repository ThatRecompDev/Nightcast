// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The ticket part of <see cref="UserTokenRequest"/>.</summary>
public sealed record UserTokenProperties(
    [property: JsonPropertyName("AuthMethod")] string AuthMethod,
    [property: JsonPropertyName("SiteName")] string SiteName,
    [property: JsonPropertyName("RpsTicket")] string RpsTicket);
