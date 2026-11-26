// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Desktop.Relay;

/// <summary>Source-generated JSON for the TURN key file and Cloudflare's credential call (NativeAOT- and trim-safe).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TurnKey))]
[JsonSerializable(typeof(TurnCredentialRequest))]
[JsonSerializable(typeof(IceServersReply))]
internal sealed partial class TurnJsonContext : JsonSerializerContext
{
}
