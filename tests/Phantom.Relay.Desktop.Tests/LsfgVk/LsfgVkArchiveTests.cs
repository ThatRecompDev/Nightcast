// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkArchiveTests
{
    [Fact]
    public void Only_the_layer_and_its_manifest_are_unpacked_in_the_archives_own_tree()
    {
        using var folder = new TempFolder();
        var destination = Path.Combine(folder.Path, "2.0.0");

        var missing = LsfgVkArchive.Unpack(new MemoryStream(FakeArchive.Release()), LsfgVkRelease.Members, destination + ".partial", destination);

        Assert.Empty(missing);
        Assert.Equal(FakeArchive.LayerText, File.ReadAllText(Path.Combine(destination, "lib", "liblsfg-vk-layer.so")));
        Assert.Equal(FakeArchive.ManifestText, File.ReadAllText(Path.Combine(destination, "share", "vulkan", "implicit_layer.d", "VkLayer_LSFGVK_frame_generation.json")));
        Assert.Equal(2, Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories).Length);
        Assert.False(Directory.Exists(destination + ".partial"));
    }

    [Fact]
    public void An_archive_without_a_member_leaves_nothing_and_says_which()
    {
        using var folder = new TempFolder();
        var destination = Path.Combine(folder.Path, "2.0.0");
        var tar = FakeArchive.Tar(("./" + LsfgVkRelease.LayerManifest, FakeArchive.ManifestText), ("./bin/lsfg-vk-cli", "cli"));

        var missing = LsfgVkArchive.Unpack(new MemoryStream(tar), LsfgVkRelease.Members, destination + ".partial", destination);

        Assert.Equal([LsfgVkRelease.LayerLibrary], missing);
        Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
    }

    [Fact]
    public void A_folder_entry_named_like_a_member_is_not_taken_and_an_older_unpack_is_replaced()
    {
        using var folder = new TempFolder();
        var destination = Path.Combine(folder.Path, "2.0.0");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "stale"), "from an interrupted run");
        var tar = FakeArchive.Tar(("./" + LsfgVkRelease.LayerLibrary, null), ("./" + LsfgVkRelease.LayerManifest, FakeArchive.ManifestText));

        Assert.Equal([LsfgVkRelease.LayerLibrary], LsfgVkArchive.Unpack(new MemoryStream(tar), LsfgVkRelease.Members, destination + ".partial", destination));
        Assert.Empty(LsfgVkArchive.Unpack(new MemoryStream(FakeArchive.Release()), LsfgVkRelease.Members, destination + ".partial", destination));
        Assert.False(File.Exists(Path.Combine(destination, "stale")));
    }

    [Fact]
    public void Names_lose_the_archives_leading_dot_folder_only()
    {
        Assert.Equal("lib/a.so", LsfgVkArchive.NameOf("./lib/a.so"));
        Assert.Equal("lib/a.so", LsfgVkArchive.NameOf("lib/a.so"));
        Assert.Equal("../a.so", LsfgVkArchive.NameOf("../a.so"));
    }
}
