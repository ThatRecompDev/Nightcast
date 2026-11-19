// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The lowest and highest version of a data channel's protocol the client speaks.</summary>
public sealed record ChannelVersion(
    [property: JsonPropertyName("minVersion")] int MinVersion,
    [property: JsonPropertyName("maxVersion")] int MaxVersion);
