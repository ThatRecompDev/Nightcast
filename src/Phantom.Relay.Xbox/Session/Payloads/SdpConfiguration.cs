// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The party-chat sound format and the version range of each data channel.</summary>
public sealed record SdpConfiguration(
    [property: JsonPropertyName("chatConfiguration")] ChatConfiguration ChatConfiguration,
    [property: JsonPropertyName("chat")] ChannelVersion Chat,
    [property: JsonPropertyName("control")] ChannelVersion Control,
    [property: JsonPropertyName("input")] ChannelVersion Input,
    [property: JsonPropertyName("message")] ChannelVersion Message);
