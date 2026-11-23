// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Security.Cryptography;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The one lsfg-vk release Nightcast uses on Linux, pinned: its version, the official build's address and SHA-256, its
/// licence, the two files of it Nightcast unpacks and the layer's name. lsfg-vk (https://lsfg-vk.dev, by PancakeTAS) is
/// a Vulkan layer that runs Lossless Scaling's frame generation from the user's own Lossless Scaling install. Since
/// 2.0.0 it is licensed CC BY-NC-ND 4.0 (its source's LICENSE.txt; 1.x was MIT/GPLv3): Nightcast never carries it, it
/// downloads this unmodified official build at run time on the player's own machine, and checks it against the hash
/// below before using it. Seen 2026-11-23: the archive at <see cref="Archive"/> hashed to <see cref="Sha256"/> (the
/// same hash the Decky plugin decky-lsfg-vk 0.14.4 pins), and holds bin/, lib/ and share/ with the two files named here.
/// </summary>
public static class LsfgVkRelease
{
    public const string Version = "2.0.0";

    /// <summary>The official build of <see cref="Version"/>, from lsfg-vk's own build server.</summary>
    public static readonly Uri Archive = new("https://builds.lsfg-vk.dev/lsfg-vk-2.0.0.tar.xz");

    /// <summary>The SHA-256 of <see cref="Archive"/>, lower-case hex: anything else is refused.</summary>
    public const string Sha256 = "08bdbdf373a111022df87dac7aa87e3b564bb841f961552e3ca85fea12b5aa74";

    public const string License = "CC BY-NC-ND 4.0";

    public const string Home = "https://lsfg-vk.dev";

    /// <summary>The 64-bit layer library, as the archive names it.</summary>
    public const string LayerLibrary = "lib/liblsfg-vk-layer.so";

    /// <summary>The layer's manifest; its <c>library_path</c> is <c>../../../lib/liblsfg-vk-layer.so</c>, relative to itself.</summary>
    public const string LayerManifest = "share/vulkan/implicit_layer.d/VkLayer_LSFGVK_frame_generation.json";

    /// <summary>The layer's name in its manifest, as the Vulkan loader lists it.</summary>
    public const string LayerName = "VK_LAYER_LSFGVK_frame_generation";

    /// <summary>The file of Lossless Scaling lsfg-vk 2.0 reads (Steam's "lsfg-vk" beta branch of Lossless Scaling has it).</summary>
    public const string LosslessFile = "lsfg-vk.dll";

    /// <summary>The name of Nightcast's profile in its own lsfg-vk configuration, chosen by <c>LSFGVK_PROFILE</c>.</summary>
    public const string ProfileName = "Nightcast";

    /// <summary>The files Nightcast takes from the archive: the 64-bit layer and its manifest, nothing else.</summary>
    public static IReadOnlyList<string> Members { get; } = [LayerLibrary, LayerManifest];

    /// <summary>The notice the menu and the console show: what this is, whose, under which licence, and what it reads.</summary>
    public static string Notice { get; } = $"Frame generation on Linux is lsfg-vk {Version} by PancakeTAS ({Home}, {License}), downloaded from its official builds and run with your own Lossless Scaling.";

    /// <summary>Whether <paramref name="archive"/>'s SHA-256 is <paramref name="expected"/> (lower-case hex; <see cref="Sha256"/> for the pinned build).</summary>
    public static bool Matches(System.IO.Stream archive, string expected)
    {
        ArgumentNullException.ThrowIfNull(archive);

        return string.Equals(Convert.ToHexStringLower(SHA256.HashData(archive)), expected, StringComparison.Ordinal);
    }
}
