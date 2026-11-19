// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>The size the client shows the stream at, and its safe area.</summary>
public sealed record DimensionsChanged(
    [property: JsonPropertyName("horizontal")] int Horizontal,
    [property: JsonPropertyName("vertical")] int Vertical,
    [property: JsonPropertyName("preferredWidth")] int PreferredWidth,
    [property: JsonPropertyName("preferredHeight")] int PreferredHeight,
    [property: JsonPropertyName("safeAreaLeft")] int SafeAreaLeft,
    [property: JsonPropertyName("safeAreaTop")] int SafeAreaTop,
    [property: JsonPropertyName("safeAreaRight")] int SafeAreaRight,
    [property: JsonPropertyName("safeAreaBottom")] int SafeAreaBottom,
    [property: JsonPropertyName("supportsCustomResolution")] bool SupportsCustomResolution);
