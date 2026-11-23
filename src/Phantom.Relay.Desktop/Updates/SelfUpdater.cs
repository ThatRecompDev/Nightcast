// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace Phantom.Relay.Desktop.Updates;

/// <summary>
/// Keeps a published Nightcast up to date from its public releases (<see cref="UpdateSource"/>), at start, before
/// anything else: GitHub's latest release is asked for anonymously; when it is newer than the running build its exe is
/// downloaded beside the running one, checked against the release's SHA-256, and swapped in (Windows lets a running exe
/// be renamed: the old one becomes <c>.old</c> and is deleted at the next start), and the new exe starts with the same
/// arguments in the same console. Offline, slow, or anything unexpected, and the app starts as it is: an update never
/// stops a stream. No credential is sent or kept.
/// </summary>
public static class SelfUpdater
{
    private const string NewSuffix = ".new";
    private const string OldSuffix = ".old";

    /// <summary>True when a newer build was started in this one's place: the caller ends now.</summary>
    public static async Task<bool> TryUpdateAsync(string? exePath, Version? running, IReadOnlyList<string> args, TextWriter output, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (exePath is null || !ReleaseChoice.Updates(running))
        {
            return false;
        }

        RemoveOld(exePath);

        try
        {
            return await UpdateAsync(exePath, running!, args, output, cancellation);
        }
        catch (Exception problem) when (problem is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            output.WriteLine($"Update check skipped ({problem.Message}).");

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

        string downloaded = exePath + NewSuffix;

        if (!await DownloadAsync(exe, downloaded, expected, cancellation))
        {
            File.Delete(downloaded);
            output.WriteLine("Update skipped: the download does not match the release's SHA-256.");

            return false;
        }

        Swap(exePath, downloaded);
        Restart(exePath, args);
        output.WriteLine($"Updated to {latest}; starting it.");

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

    private static void Swap(string exePath, string downloaded)
    {
        string old = exePath + OldSuffix;
        File.Move(exePath, old, overwrite: true);
        File.Move(downloaded, exePath);
    }

    private static void Restart(string exePath, IReadOnlyList<string> args)
    {
        var start = new ProcessStartInfo(exePath) { UseShellExecute = false };

        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);
    }

    /// <summary>The build an update replaced, still there from the last start; gone once nothing runs it.</summary>
    private static void RemoveOld(string exePath)
    {
        try
        {
            File.Delete(exePath + OldSuffix);
        }
        catch (IOException)
        {
            // Still running (a second window): the next start removes it.
        }
        catch (UnauthorizedAccessException)
        {
            // The same.
        }
    }
}
