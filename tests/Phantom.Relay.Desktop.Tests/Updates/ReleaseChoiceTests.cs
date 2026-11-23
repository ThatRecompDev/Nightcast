// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Updates;

namespace Phantom.Relay.Desktop.Tests.Updates;

public sealed class ReleaseChoiceTests
{
    private static readonly string Folder = $"{UpdateSource.DownloadPrefix}v2026.11.23.1135/";

    [Fact]
    public void A_release_tag_is_its_version_and_only_published_builds_update()
    {
        Assert.Equal(new Version(2026, 11, 23, 1135), ReleaseChoice.VersionOf(new GitHubRelease("v2026.11.23.1135", [])));
        Assert.Null(ReleaseChoice.VersionOf(new GitHubRelease("latest", [])));

        Assert.True(ReleaseChoice.Updates(new Version(2026, 11, 23, 1135)));
        Assert.False(ReleaseChoice.Updates(new Version(1, 0, 0, 0)));
        Assert.False(ReleaseChoice.Updates(null));
    }

    [Fact]
    public void The_files_come_only_from_the_releases_own_folder()
    {
        var release = new GitHubRelease("v2026.11.23.1135", [
            new GitHubAsset(UpdateSource.ExeName, Folder + UpdateSource.ExeName),
            new GitHubAsset(UpdateSource.ChecksumName, Folder + UpdateSource.ChecksumName),
        ]);
        var elsewhere = new GitHubRelease("v2026.11.23.1135", [
            new GitHubAsset(UpdateSource.ExeName, "https://example.test/" + UpdateSource.ExeName),
            new GitHubAsset(UpdateSource.ChecksumName, Folder + UpdateSource.ChecksumName),
        ]);

        Assert.True(ReleaseChoice.TryFiles(release, out var exe, out _));
        Assert.Equal(new Uri(Folder + UpdateSource.ExeName), exe);
        Assert.False(ReleaseChoice.TryFiles(elsewhere, out _, out _));
    }

    [Fact]
    public void A_checksum_file_names_its_sha256_first()
    {
        string hash = new('A', 64);

        Assert.Equal(new string('a', 64), ReleaseChoice.ChecksumOf($"{hash}  Nightcast.exe\n"));
        Assert.Null(ReleaseChoice.ChecksumOf("not a hash"));
    }
}
