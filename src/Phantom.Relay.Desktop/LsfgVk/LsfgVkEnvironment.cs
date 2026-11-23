// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// What Nightcast sets in its own environment, before the window's Vulkan instance exists, so the loader finds
/// lsfg-vk in Nightcast's folder and the layer takes Nightcast's profile, as the Decky plugin's <c>~/.lsfg</c> wrapper
/// sets them for a game: the manifest's folder added to the loader's implicit layers
/// (<c>VK_ADD_IMPLICIT_LAYER_PATH</c>; loaders older than it read <c>XDG_DATA_DIRS</c>, <see cref="AddDataFolder"/>),
/// <c>LSFGVK_CONFIG</c> and <c>LSFGVK_PROFILE</c> (the profile by name: a non-Steam shortcut has no Steam app id of its
/// own), and the wrapper's defaults <c>ENABLE_GAMESCOPE_WSI=0</c> (gamescope's own Vulkan layer stays out of the
/// swapchain lsfg-vk wraps) and <c>DXVK_HDR=0</c> (DXVK's; inert for Nightcast, kept as the wrapper has it); the
/// layer's off switches (<c>DISABLE_LSFGVK</c>, the manifest's disable variable, and 1.x's <c>DISABLE_LSFG</c>) and
/// <c>LSFGVK_ENV</c> (configuration from variables instead of the file) are removed. Only this process and what it
/// starts see them.
/// </summary>
public static class LsfgVkEnvironment
{
    public const string ImplicitLayerPath = "VK_ADD_IMPLICIT_LAYER_PATH";
    public const string DataFolders = "XDG_DATA_DIRS";
    public const string Config = "LSFGVK_CONFIG";
    public const string Profile = "LSFGVK_PROFILE";
    public const string GamescopeWsi = "ENABLE_GAMESCOPE_WSI";
    public const string DxvkHdr = "DXVK_HDR";

    /// <summary>The XDG base directory specification's data folders when <c>XDG_DATA_DIRS</c> is unset.</summary>
    public const string DefaultDataFolders = "/usr/local/share:/usr/share";

    private const char Separator = ':';
    private const string Off = "0";

    private static readonly string[] Removed = ["DISABLE_LSFGVK", "DISABLE_LSFG", "LSFGVK_ENV"];

    /// <summary>Sets the layer's variables for the version in <paramref name="folders"/>.</summary>
    public static void Apply(IProcessEnvironment environment, LsfgVkFolders folders)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(folders);

        environment.Set(ImplicitLayerPath, Prepended(folders.ManifestFolder, environment.Get(ImplicitLayerPath), null));
        environment.Set(Config, folders.ConfigPath);
        environment.Set(Profile, LsfgVkRelease.ProfileName);
        environment.Set(GamescopeWsi, Off);
        environment.Set(DxvkHdr, Off);

        foreach (var name in Removed)
        {
            environment.Unset(name);
        }
    }

    /// <summary>
    /// For a loader without <c>VK_ADD_IMPLICIT_LAYER_PATH</c>: the version's <c>share</c> folder first among the data
    /// folders, whose <c>vulkan/implicit_layer.d</c> every loader searches (the default folders kept after it).
    /// </summary>
    public static void AddDataFolder(IProcessEnvironment environment, LsfgVkFolders folders)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(folders);
        environment.Set(DataFolders, Prepended(folders.DataFolder, environment.Get(DataFolders), DefaultDataFolders));
    }

    /// <summary><paramref name="folder"/> before the list <paramref name="current"/> (else <paramref name="fallback"/>); not twice.</summary>
    public static string Prepended(string folder, string? current, string? fallback)
    {
        var rest = current;

        if (string.IsNullOrEmpty(rest))
        {
            rest = fallback;
        }

        if (string.IsNullOrEmpty(rest))
        {
            return folder;
        }

        if (rest.Split(Separator).Contains(folder, StringComparer.Ordinal))
        {
            return rest;
        }

        return folder + Separator + rest;
    }
}
