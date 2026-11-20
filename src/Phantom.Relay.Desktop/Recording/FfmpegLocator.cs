// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// Finds the user's own ffmpeg at run time (never bundled): the first <c>ffmpeg.exe</c> in a folder the PATH lists,
/// else scoop's in the user's profile (<c>scoop\apps\ffmpeg\current\bin\ffmpeg.exe</c>); none, and the comparison is
/// skipped with a reason.
/// </summary>
internal static class FfmpegLocator
{
    public const string Executable = "ffmpeg.exe";

    /// <summary>Why there is no comparison when none is found.</summary>
    public const string NotFound = "ffmpeg not found (on the PATH or in scoop)";

    private static readonly string[] ScoopFolders = ["scoop", "apps", "ffmpeg", "current", "bin"];

    /// <summary>
    /// ffmpeg's path from the PATH's value <paramref name="path"/> (may be null) and the user's profile folder
    /// <paramref name="profile"/>, as <paramref name="exists"/> finds files; null when there is none.
    /// </summary>
    public static string? Find(string? path, string profile, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        foreach (var folder in (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(folder.Trim('"'), Executable);

            if (exists(candidate))
            {
                return candidate;
            }
        }

        if (string.IsNullOrEmpty(profile))
        {
            return null;
        }

        var scoop = Path.Combine([profile, .. ScoopFolders, Executable]);

        if (exists(scoop))
        {
            return scoop;
        }

        return null;
    }
}
