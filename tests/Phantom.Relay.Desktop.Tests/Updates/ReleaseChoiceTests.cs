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

        Assert.True(ReleaseChoice.TryFiles(release, UpdateAssets.Windows, out var exe, out _));
        Assert.Equal(new Uri(Folder + UpdateSource.ExeName), exe);
        Assert.False(ReleaseChoice.TryFiles(elsewhere, UpdateAssets.Windows, out _, out _));
    }

    [Fact]
    public void Each_system_takes_its_own_build_from_a_release_of_both()
    {
        var release = new GitHubRelease("v2026.11.23.1135", [
            new GitHubAsset(UpdateSource.ExeName, Folder + UpdateSource.ExeName),
            new GitHubAsset(UpdateSource.ChecksumName, Folder + UpdateSource.ChecksumName),
            new GitHubAsset(UpdateSource.LinuxName, Folder + UpdateSource.LinuxName),
            new GitHubAsset(UpdateSource.LinuxChecksumName, Folder + UpdateSource.LinuxChecksumName),
        ]);

        Assert.True(ReleaseChoice.TryFiles(release, UpdateAssets.Linux, out var linux, out var linuxChecksum));
        Assert.True(ReleaseChoice.TryFiles(release, UpdateAssets.Windows, out var windows, out _));

        Assert.Equal(new Uri(Folder + "Nightcast-linux-x64"), linux);
        Assert.Equal(new Uri(Folder + "Nightcast-linux-x64.sha256"), linuxChecksum);
        Assert.Equal(new Uri(Folder + "Nightcast.exe"), windows);
    }

    [Fact]
    public void A_release_without_the_systems_build_offers_no_update()
    {
        var windowsOnly = new GitHubRelease("v2026.11.23.1135", [
            new GitHubAsset(UpdateSource.ExeName, Folder + UpdateSource.ExeName),
            new GitHubAsset(UpdateSource.ChecksumName, Folder + UpdateSource.ChecksumName),
        ]);

        Assert.False(ReleaseChoice.TryFiles(windowsOnly, UpdateAssets.Linux, out _, out _));
    }

    [Fact]
    public void The_running_system_picks_its_build_and_another_system_has_none()
    {
        Assert.Same(UpdateAssets.Windows, UpdateAssets.For(windows: true, linux: false));
        Assert.Same(UpdateAssets.Linux, UpdateAssets.For(windows: false, linux: true));
        Assert.Null(UpdateAssets.For(windows: false, linux: false));
    }

    [Fact]
    public void A_build_handed_over_on_linux_is_made_executable_for_everyone()
    {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;

        var executable = UpdateHandoff.Executable(mode);

        Assert.Equal(mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute, executable);
    }

    [Fact]
    public void A_checksum_file_names_its_sha256_first()
    {
        string hash = new('A', 64);

        Assert.Equal(new string('a', 64), ReleaseChoice.ChecksumOf($"{hash}  Nightcast.exe\n"));
        Assert.Null(ReleaseChoice.ChecksumOf("not a hash"));
    }
}
