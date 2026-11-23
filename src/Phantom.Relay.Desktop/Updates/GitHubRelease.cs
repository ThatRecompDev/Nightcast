// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;

namespace Phantom.Relay.Desktop.Updates;

/// <summary>The fields of GitHub's release answer the updater reads: the tag and the files.</summary>
public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset>? Assets);
