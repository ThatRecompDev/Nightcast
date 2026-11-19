// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Auth.Payloads;

/// <summary>The regions an offering is served from.</summary>
public sealed record OfferingSettings(
    [property: JsonPropertyName("regions")] IReadOnlyList<RegionReply>? Regions);
