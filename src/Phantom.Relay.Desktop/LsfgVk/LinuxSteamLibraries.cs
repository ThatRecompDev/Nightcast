// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>The Steam libraries of a Linux Steam (SteamOS's): its own folder and the ones its libraryfolders.vdf lists.</summary>
public static class LinuxSteamLibraries
{
    /// <summary>Where Steam keeps itself on Linux, below the home folder (SteamOS: the first; older installs: the second).</summary>
    private static readonly string[] SteamFolders = [Path.Combine(".local", "share", "Steam"), Path.Combine(".steam", "steam")];

    /// <summary>The libraries below <paramref name="home"/>; none without a home.</summary>
    public static List<string> Below(string? home)
    {
        var libraries = new List<string>();

        if (string.IsNullOrEmpty(home))
        {
            return libraries;
        }

        foreach (var steam in SteamFolders)
        {
            var root = Path.Combine(home, steam);
            var folders = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            libraries.Add(root);

            if (File.Exists(folders))
            {
                libraries.AddRange(LosslessLocator.ParseLibraryFolders(File.ReadAllText(folders)));
            }
        }

        return libraries;
    }
}
