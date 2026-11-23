// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// Where Nightcast keeps lsfg-vk: its own data folder (<c>$XDG_DATA_HOME</c>, else <c>~/.local/share</c>, then
/// <c>Nightcast/lsfg-vk</c>), never the shared <c>~/.local/lib</c> or <c>implicit_layer.d</c>, so no other program
/// loads it. Each version unpacks below its own folder with the archive's own tree (<c>lib/</c> beside
/// <c>share/vulkan/implicit_layer.d/</c>, as the manifest's relative library path needs); Nightcast's configuration
/// and the layer's log sit beside the versions.
/// </summary>
public sealed record LsfgVkFolders(string Base)
{
    private const string AppFolder = "Nightcast";
    private const string ToolFolder = "lsfg-vk";
    private const string ConfigFile = "conf.toml";
    private const string LogFile = "lsfg-vk.log";
    private const string DownloadSuffix = ".download";
    private const string TarSuffix = ".tar";
    private const string PartialSuffix = ".partial";

    /// <summary>This version's folder, holding the archive's tree.</summary>
    public string Root => Path.Combine(Base, LsfgVkRelease.Version);

    /// <summary>The folder the layer's manifest is in, for the Vulkan loader.</summary>
    public string ManifestFolder => Path.GetDirectoryName(ManifestPath)!;

    public string ManifestPath => Path.Combine(Root, LsfgVkRelease.LayerManifest);

    public string LibraryPath => Path.Combine(Root, LsfgVkRelease.LayerLibrary);

    /// <summary>The <c>share</c> folder of this version: a data folder whose <c>vulkan/implicit_layer.d</c> holds the manifest.</summary>
    public string DataFolder => Path.Combine(Root, "share");

    /// <summary>Nightcast's own lsfg-vk configuration (<c>LSFGVK_CONFIG</c>).</summary>
    public string ConfigPath => Path.Combine(Base, ConfigFile);

    /// <summary>Where the layer writes its log (the configuration's <c>log_file</c>).</summary>
    public string LogPath => Path.Combine(Base, LogFile);

    /// <summary>The archive while it downloads.</summary>
    public string DownloadPath => Path.Combine(Base, $"lsfg-vk-{LsfgVkRelease.Version}{DownloadSuffix}");

    /// <summary>The archive decompressed, while it unpacks.</summary>
    public string TarPath => DownloadPath + TarSuffix;

    /// <summary>The version's folder while it unpacks; it becomes <see cref="Root"/> once whole.</summary>
    public string PartialRoot => Root + PartialSuffix;

    /// <summary>Whether this version's layer and manifest are unpacked.</summary>
    public bool Installed => File.Exists(LibraryPath) && File.Exists(ManifestPath);

    /// <summary>The folders under the user's data home: <paramref name="dataHome"/>, else <paramref name="home"/>'s <c>.local/share</c>; null without either.</summary>
    public static LsfgVkFolders? For(string? dataHome, string? home)
    {
        if (!string.IsNullOrEmpty(dataHome))
        {
            return new LsfgVkFolders(Path.Combine(dataHome, AppFolder, ToolFolder));
        }

        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        return new LsfgVkFolders(Path.Combine(home, ".local", "share", AppFolder, ToolFolder));
    }
}
