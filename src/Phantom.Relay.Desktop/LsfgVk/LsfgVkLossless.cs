// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The player's own Lossless Scaling file lsfg-vk 2.0 reads (<see cref="LsfgVkRelease.LosslessFile"/>, found in Steam's
/// libraries), or why there is none: Lossless Scaling missing, or installed without its "lsfg-vk" beta branch (which
/// brings that file).
/// </summary>
public sealed record LsfgVkLossless(string? Path, string? Problem)
{
    /// <summary>The file in the first of <paramref name="libraries"/> that has it.</summary>
    public static LsfgVkLossless Find(IEnumerable<string> libraries)
    {
        var searched = libraries.ToList();
        var file = LosslessLocator.Find(null, searched, LosslessLocator.LsfgVkRelativePath);

        if (file is not null)
        {
            return new LsfgVkLossless(file, null);
        }

        if (LosslessLocator.Find(null, searched) is not null)
        {
            return new LsfgVkLossless(null, "Lossless Scaling is installed without its lsfg-vk branch (Steam: Lossless Scaling, Properties, Betas, lsfg-vk)");
        }

        return new LsfgVkLossless(null, "Lossless Scaling was not found in Steam (lsfg-vk runs it: buy and install it in Steam, branch lsfg-vk)");
    }
}
