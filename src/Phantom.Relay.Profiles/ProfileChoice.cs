// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>Picks the profile for the running title: the first that names it, else the default profile.</summary>
public static class ProfileChoice
{
    public static StreamProfile For(IReadOnlyList<StreamProfile> profiles, string? titleId)
    {
        StreamProfile? fallback = null;

        foreach (var profile in profiles)
        {
            if (titleId is not null && Names(profile, titleId))
            {
                return profile;
            }

            if (fallback is null && profile.IsDefault)
            {
                fallback = profile;
            }
        }

        return fallback ?? StreamProfile.Default;
    }

    private static bool Names(StreamProfile profile, string titleId)
    {
        foreach (string id in profile.TitleIds)
        {
            if (string.Equals(id, titleId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
