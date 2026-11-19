// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phantom.Relay.Profiles;

/// <summary>Source-generated JSON for profiles: camelCase names, enums by name, indented for hand editing.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(StreamProfile))]
public sealed partial class ProfilesJsonContext : JsonSerializerContext
{
}
