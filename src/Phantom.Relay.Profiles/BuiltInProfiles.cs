// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles.KeyboardMouse;

namespace Phantom.Relay.Profiles;

/// <summary>
/// The profiles Relay ships, beside the player's own: a stored profile of the same name replaces the built-in one, so a
/// player's tuning wins and a built-in comes back when the player deletes theirs.
/// </summary>
public static class BuiltInProfiles
{
    public static IReadOnlyList<StreamProfile> All { get; } = [GtaKeyboardMousePreset.Profile];

    /// <summary>The player's profiles, then every built-in one the player has none of the same name for.</summary>
    public static IReadOnlyList<StreamProfile> With(IReadOnlyList<StreamProfile> stored)
    {
        var profiles = new List<StreamProfile>(stored);

        foreach (var builtIn in All)
        {
            if (!profiles.Exists(profile => string.Equals(profile.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase)))
            {
                profiles.Add(builtIn);
            }
        }

        return profiles;
    }
}
