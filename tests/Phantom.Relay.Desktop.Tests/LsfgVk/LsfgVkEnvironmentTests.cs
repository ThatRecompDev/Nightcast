// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkEnvironmentTests
{
    private static readonly LsfgVkFolders Folders = new("/data/player/Nightcast/lsfg-vk");

    [Fact]
    public void The_layers_folder_config_and_profile_are_set_and_its_off_switches_removed()
    {
        var environment = new FakeEnvironment();
        environment.Set("DISABLE_LSFGVK", "1");
        environment.Set("DISABLE_LSFG", "1");
        environment.Set("LSFGVK_ENV", "1");
        environment.Set(LsfgVkEnvironment.ImplicitLayerPath, "/opt/layers");

        LsfgVkEnvironment.Apply(environment, Folders);

        Assert.Equal(
            new Dictionary<string, string>
            {
                [LsfgVkEnvironment.ImplicitLayerPath] = Folders.ManifestFolder + ":/opt/layers",
                [LsfgVkEnvironment.Config] = Folders.ConfigPath,
                [LsfgVkEnvironment.Profile] = "Nightcast",
                [LsfgVkEnvironment.GamescopeWsi] = "0",
                [LsfgVkEnvironment.DxvkHdr] = "0",
            },
            environment.Values);
    }

    [Fact]
    public void An_older_loader_finds_the_layer_through_the_data_folders_the_defaults_kept()
    {
        var environment = new FakeEnvironment();

        LsfgVkEnvironment.AddDataFolder(environment, Folders);
        LsfgVkEnvironment.AddDataFolder(environment, Folders);

        Assert.Equal(Folders.DataFolder + ":/usr/local/share:/usr/share", environment.Get(LsfgVkEnvironment.DataFolders));
    }

    [Fact]
    public void A_folder_goes_first_once_and_alone_into_an_empty_list()
    {
        Assert.Equal("/a", LsfgVkEnvironment.Prepended("/a", null, null));
        Assert.Equal("/a", LsfgVkEnvironment.Prepended("/a", string.Empty, null));
        Assert.Equal("/a:/b", LsfgVkEnvironment.Prepended("/a", "/b", "/c"));
        Assert.Equal("/b:/a", LsfgVkEnvironment.Prepended("/a", "/b:/a", null));
    }
}
