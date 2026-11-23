// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Desktop.Updates;

/// <summary>
/// The pure part of the update: whether a release is newer than the running build, and its exe's and checksum's
/// download addresses, only from below <see cref="UpdateSource.DownloadPrefix"/>. A development build (version 0 or
/// 1.0.0.0, not made by the publish script) never updates.
/// </summary>
public static class ReleaseChoice
{
    private static readonly Version DevelopmentBuild = new(1, 0, 0, 0);

    /// <summary>The release's version from its tag (v2026.11.23.1135); null when the tag is not one.</summary>
    public static Version? VersionOf(GitHubRelease release)
    {
        ArgumentNullException.ThrowIfNull(release);
        var tag = release.TagName;

        if (tag is null || !tag.StartsWith(UpdateSource.TagPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        if (!Version.TryParse(tag[UpdateSource.TagPrefix.Length..], out var version))
        {
            return null;
        }

        return version;
    }

    /// <summary>True when the running build updates at all: a published build, not one from the IDE.</summary>
    public static bool Updates(Version? running)
    {
        return running is not null && running > DevelopmentBuild;
    }

    /// <summary>The exe's and the checksum's addresses when the release has both, from the release's own folder.</summary>
    public static bool TryFiles(GitHubRelease release, out Uri exe, out Uri checksum)
    {
        ArgumentNullException.ThrowIfNull(release);
        exe = checksum = null!;

        var exeUrl = Find(release, UpdateSource.ExeName);
        var checksumUrl = Find(release, UpdateSource.ChecksumName);

        if (exeUrl is null || checksumUrl is null)
        {
            return false;
        }

        exe = new Uri(exeUrl);
        checksum = new Uri(checksumUrl);

        return true;
    }

    /// <summary>The SHA-256 a checksum file names first (64 hex digits), lower case; null when it names none.</summary>
    public static string? ChecksumOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var first = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        if (first is null || first.Length != Sha256HexLength || !first.All(Uri.IsHexDigit))
        {
            return null;
        }

        return first.ToLower(CultureInfo.InvariantCulture);
    }

    private const int Sha256HexLength = 64;

    private static string? Find(GitHubRelease release, string name)
    {
        foreach (var asset in release.Assets ?? [])
        {
            string? url = asset.DownloadUrl;

            if (asset.Name == name && url is not null && url.StartsWith(UpdateSource.DownloadPrefix, StringComparison.Ordinal))
            {
                return url;
            }
        }

        return null;
    }
}
