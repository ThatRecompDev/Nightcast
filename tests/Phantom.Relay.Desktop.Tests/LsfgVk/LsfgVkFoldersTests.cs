// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkFoldersTests
{
    [Fact]
    public void The_data_home_holds_Nightcasts_own_lsfg_vk_else_the_homes_local_share_and_without_either_none()
    {
        Assert.Equal(Path.Combine("/data", "Nightcast", "lsfg-vk"), LsfgVkFolders.For("/data", "/home/player")!.Base);
        Assert.Equal(Path.Combine("/home/player", ".local", "share", "Nightcast", "lsfg-vk"), LsfgVkFolders.For(string.Empty, "/home/player")!.Base);
        Assert.Null(LsfgVkFolders.For(null, null));
    }

    [Fact]
    public void Each_version_keeps_the_archives_tree_so_the_manifests_relative_library_path_resolves()
    {
        var folders = new LsfgVkFolders("/base");

        Assert.Equal("/base/2.0.0/share/vulkan/implicit_layer.d", Slashed(folders.ManifestFolder));
        Assert.Equal("/base/2.0.0/lib/liblsfg-vk-layer.so", Slashed(folders.LibraryPath));
        Assert.Equal("/base/2.0.0/share", Slashed(folders.DataFolder));
        Assert.Equal("/base/conf.toml", Slashed(folders.ConfigPath));
    }

    // The tests also run on Windows, whose paths take backslashes.
    private static string Slashed(string path)
    {
        return path.Replace('\\', '/');
    }
}
