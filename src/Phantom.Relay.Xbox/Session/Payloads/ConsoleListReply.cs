// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Xbox.Session.Payloads;

/// <summary>The consoles the account can stream from.</summary>
public sealed record ConsoleListReply(
    [property: JsonPropertyName("results")] IReadOnlyList<ConsoleReply>? Results);
