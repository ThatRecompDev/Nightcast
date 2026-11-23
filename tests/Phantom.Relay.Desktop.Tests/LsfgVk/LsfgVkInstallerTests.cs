// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Security.Cryptography;
using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkInstallerTests
{
    [Fact]
    public async Task The_pinned_archive_is_downloaded_once_unpacked_and_its_download_removed()
    {
        using var folder = new TempFolder();
        var folders = new LsfgVkFolders(folder.Path);
        var archive = FakeArchive.Release();
        var source = new BytesSource(archive);
        var installer = new LsfgVkInstaller(folders, source, new CopyDecompressor(), Convert.ToHexStringLower(SHA256.HashData(archive)));

        Assert.Null(await installer.EnsureAsync(CancellationToken.None));
        Assert.Null(await installer.EnsureAsync(CancellationToken.None));

        Assert.True(folders.Installed);
        Assert.Equal(1, source.Downloads);
        Assert.Equal(LsfgVkRelease.Archive, source.Asked);
        Assert.False(File.Exists(folders.DownloadPath));
        Assert.False(File.Exists(folders.TarPath));
    }

    [Fact]
    public async Task An_archive_whose_SHA_256_differs_from_the_pin_is_refused_and_nothing_is_unpacked()
    {
        using var folder = new TempFolder();
        var folders = new LsfgVkFolders(folder.Path);
        var installer = new LsfgVkInstaller(folders, new BytesSource(FakeArchive.Release()), new CopyDecompressor(), LsfgVkRelease.Sha256);

        var problem = await installer.EnsureAsync(CancellationToken.None);

        Assert.NotNull(problem);
        Assert.Contains("SHA-256 differs", problem, StringComparison.Ordinal);
        Assert.False(folders.Installed);
        Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
    }
}
