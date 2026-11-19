// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Channels.Payloads;

/// <summary>Tells the console a pad slot was filled or emptied.</summary>
public sealed record ControlGamepadChanged(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("gamepadIndex")] int GamepadIndex,
    [property: JsonPropertyName("wasAdded")] bool WasAdded);
