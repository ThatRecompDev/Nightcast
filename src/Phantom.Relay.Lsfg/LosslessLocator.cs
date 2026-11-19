// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Win32;

namespace Phantom.Relay.Lsfg;

/// <summary>
/// Finds the user's own Lossless.dll: the configured path when it exists, otherwise Lossless Scaling's folder in
/// each Steam library that Steam's libraryfolders.vdf lists. No path is stored in the repository; the libraries are
/// read from the user's Steam install at run time.
/// </summary>
public static class LosslessLocator
{
    /// <summary>Lossless Scaling's Steam folder and its frame generation library, below a Steam library's root.</summary>
    public static readonly string RelativePath = Path.Combine("steamapps", "common", "Lossless Scaling", "Lossless.dll");

    // Steam's install folder, written by Steam's own installer for the user.
    private const string SteamKey = @"Software\Valve\Steam";
    private const string SteamPathValue = "SteamPath";
    private const string PathKey = "\"path\"";
    private const char Quote = '"';

    /// <summary>The configured path when the file is there, else the first Steam library holding it, else null.</summary>
    public static string? Find(string? configured, IEnumerable<string> steamLibraries)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        foreach (var library in steamLibraries)
        {
            var candidate = Path.Combine(library, RelativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The Steam libraries of this user's Steam install (empty when Steam is not installed).</summary>
    public static IReadOnlyList<string> SteamLibraries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        var install = key?.GetValue(SteamPathValue) as string;

        if (string.IsNullOrEmpty(install))
        {
            return [];
        }

        var folders = Path.Combine(install, "steamapps", "libraryfolders.vdf");

        if (!File.Exists(folders))
        {
            return [install];
        }

        return ParseLibraryFolders(File.ReadAllText(folders));
    }

    /// <summary>The "path" values of a libraryfolders.vdf, unescaped, in file order.</summary>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdf)
    {
        var libraries = new List<string>();

        foreach (var raw in vdf.Split('\n'))
        {
            var line = raw.Trim();

            if (!line.StartsWith(PathKey, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[PathKey.Length..].Trim().Trim(Quote);

            if (value.Length > 0)
            {
                libraries.Add(value.Replace(@"\\", @"\", StringComparison.Ordinal));
            }
        }

        return libraries;
    }
}
