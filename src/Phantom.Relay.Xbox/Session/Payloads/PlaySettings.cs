// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The client settings of <see cref="PlayRequest"/>; <see cref="OsName"/> chooses the stream tier.</summary>
public sealed record PlaySettings(
    [property: JsonPropertyName("nanoVersion")] string NanoVersion,
    [property: JsonPropertyName("enableTextToSpeech")] bool EnableTextToSpeech,
    [property: JsonPropertyName("highContrast")] int HighContrast,
    [property: JsonPropertyName("locale")] string Locale,
    [property: JsonPropertyName("useIceConnection")] bool UseIceConnection,
    [property: JsonPropertyName("timezoneOffsetMinutes")] int TimezoneOffsetMinutes,
    [property: JsonPropertyName("sdkType")] string SdkType,
    [property: JsonPropertyName("osName")] string OsName);
