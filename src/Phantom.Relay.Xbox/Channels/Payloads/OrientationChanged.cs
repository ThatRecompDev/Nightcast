// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>The screen's orientation (0: landscape).</summary>
public sealed record OrientationChanged(
    [property: JsonPropertyName("orientation")] int Orientation);
