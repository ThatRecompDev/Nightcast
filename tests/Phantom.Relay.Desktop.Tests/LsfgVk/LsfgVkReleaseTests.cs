// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Security.Cryptography;
using System.Text;
using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkReleaseTests
{
    [Fact]
    public void The_pin_is_one_version_on_lsfg_vks_own_build_server_with_a_SHA_256()
    {
        Assert.Equal("2.0.0", LsfgVkRelease.Version);
        Assert.Equal("https://builds.lsfg-vk.dev/lsfg-vk-2.0.0.tar.xz", LsfgVkRelease.Archive.AbsoluteUri);
        Assert.Matches("^[0-9a-f]{64}$", LsfgVkRelease.Sha256);
        Assert.Equal([LsfgVkRelease.LayerLibrary, LsfgVkRelease.LayerManifest], LsfgVkRelease.Members);
    }

    [Fact]
    public void Only_bytes_with_the_expected_hash_match()
    {
        var bytes = Encoding.UTF8.GetBytes("an archive");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        Assert.True(LsfgVkRelease.Matches(new MemoryStream(bytes), hash));
        Assert.False(LsfgVkRelease.Matches(new MemoryStream(bytes), LsfgVkRelease.Sha256));
        Assert.False(LsfgVkRelease.Matches(new MemoryStream(bytes), hash.ToUpperInvariant()));
    }

    [Fact]
    public void The_notice_names_lsfg_vk_its_licence_and_the_players_own_Lossless_Scaling()
    {
        Assert.Contains("lsfg-vk 2.0.0", LsfgVkRelease.Notice, StringComparison.Ordinal);
        Assert.Contains("CC BY-NC-ND 4.0", LsfgVkRelease.Notice, StringComparison.Ordinal);
        Assert.Contains("https://lsfg-vk.dev", LsfgVkRelease.Notice, StringComparison.Ordinal);
        Assert.Contains("your own Lossless Scaling", LsfgVkRelease.Notice, StringComparison.Ordinal);
    }
}
