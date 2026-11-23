// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Net.Http.Json;
using System.Security.Cryptography;

namespace Phantom.Relay.Desktop.Updates;

/// <summary>
/// Keeps a published Nightcast up to date from its public releases (<see cref="UpdateSource"/>), at start, before
/// anything else: GitHub's latest release is asked for anonymously; when it is newer than the running build its exe is
/// downloaded beside the running one as <c>.new</c> and checked against the release's SHA-256, and the new exe is
/// started to finish the update (<see cref="UpdateHandoff"/>) while this one ends. The running exe's file is never
/// renamed or replaced while it runs: a single-file app loads parts of itself from its own file later, by path, and
/// another file at that path breaks it. Offline, slow, or anything unexpected, and the app starts as it is: an update
/// never stops a stream. No credential is sent or kept.
/// </summary>
public static class SelfUpdater
{
    /// <summary>True when a newer build was started to take this one's place: the caller ends now.</summary>
    public static async Task<bool> TryUpdateAsync(string? exePath, Version? running, IReadOnlyList<string> args, TextWriter output, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (exePath is null || !ReleaseChoice.Updates(running))
        {
            return false;
        }

        UpdateHandoff.RemoveLeftover(exePath);

        try
        {
            return await UpdateAsync(exePath, running!, args, output, cancellation);
        }
        catch (Exception problem) when (problem is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            output.WriteLine($"Update check skipped ({problem.GetType().Name}: {problem.Message}).");

            return false;
        }
    }

    private static async Task<bool> UpdateAsync(string exePath, Version running, IReadOnlyList<string> args, TextWriter output, CancellationToken cancellation)
    {
        using var client = new HttpClient { Timeout = UpdateSource.CheckTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UpdateSource.UserAgent);

        var release = await client.GetFromJsonAsync(UpdateSource.LatestRelease, UpdateJsonContext.Default.GitHubRelease, cancellation);
        var latest = release is null ? null : ReleaseChoice.VersionOf(release);

        if (release is null || latest is null || latest <= running || !ReleaseChoice.TryFiles(release, out var exe, out var checksum))
        {
            output.WriteLine($"{UpdateSource.ExeName} {running} is up to date.");

            return false;
        }

        output.WriteLine($"Updating to {latest}...");
        string? expected = ReleaseChoice.ChecksumOf(await client.GetStringAsync(checksum, cancellation));

        if (expected is null)
        {
            output.WriteLine("Update skipped: the release's checksum file is not a SHA-256.");

            return false;
        }

        string downloaded = UpdateHandoff.NewPath(exePath);

        if (!await DownloadAsync(exe, downloaded, expected, cancellation))
        {
            File.Delete(downloaded);
            output.WriteLine("Update skipped: the download does not match the release's SHA-256.");

            return false;
        }

        UpdateHandoff.Start(downloaded, exePath, args);
        output.WriteLine($"Downloaded {latest}; it takes over now.");

        return true;
    }

    /// <summary>Downloads the exe and checks its SHA-256; false when it differs.</summary>
    private static async Task<bool> DownloadAsync(Uri exe, string path, string expected, CancellationToken cancellation)
    {
        using var client = new HttpClient { Timeout = UpdateSource.DownloadTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UpdateSource.UserAgent);

        await using (var source = await client.GetStreamAsync(exe, cancellation))
        await using (var file = File.Create(path))
        {
            await source.CopyToAsync(file, cancellation);
        }

        await using var written = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(written, cancellation);

        return Convert.ToHexStringLower(hash) == expected;
    }
}
