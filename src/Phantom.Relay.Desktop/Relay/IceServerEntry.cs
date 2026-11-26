// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;

namespace Phantom.Relay.Desktop.Relay;

/// <summary>One RTCIceServer: its URLs (one string or an array of them), and the credential they share.</summary>
internal sealed record IceServerEntry(JsonElement Urls, string? Username, string? Credential);
