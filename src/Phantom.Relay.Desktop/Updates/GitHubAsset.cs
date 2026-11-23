// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Desktop.Updates;

/// <summary>One file of a GitHub release: its name and where it downloads from.</summary>
public sealed record GitHubAsset(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("browser_download_url")] string? DownloadUrl);
