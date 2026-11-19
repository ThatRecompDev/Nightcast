// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>The id of this installation of the client.</summary>
public sealed record ClientInstallChanged(
    [property: JsonPropertyName("clientAppInstallId")] string ClientAppInstallId);
