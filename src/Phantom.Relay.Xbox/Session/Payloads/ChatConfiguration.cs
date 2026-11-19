// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The microphone sound the client would send for party chat.</summary>
public sealed record ChatConfiguration(
    [property: JsonPropertyName("bytesPerSample")] int BytesPerSample,
    [property: JsonPropertyName("expectedClipDurationMs")] int ExpectedClipDurationMs,
    [property: JsonPropertyName("format")] ChatFormat Format,
    [property: JsonPropertyName("numChannels")] int NumChannels,
    [property: JsonPropertyName("sampleFrequencyHz")] int SampleFrequencyHz);
