// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles.KeyboardMouse;

namespace Phantom.Relay.Profiles.Tests;

public sealed class BuiltInProfilesTests
{
    [Fact]
    public void Gta_six_on_the_console_picks_the_keyboard_and_mouse_preset()
    {
        var profiles = BuiltInProfiles.With([]);

        var chosen = ProfileChoice.For(profiles, "7952987A");

        Assert.Same(GtaKeyboardMousePreset.Profile, chosen);
        Assert.True(chosen.KeyboardMouse.Enabled);
    }

    [Fact]
    public void Another_title_plays_the_default_profile()
    {
        var chosen = ProfileChoice.For(BuiltInProfiles.With([]), "00000000");

        Assert.Same(StreamProfile.Default, chosen);
    }

    [Fact]
    public void A_stored_profile_of_the_same_name_replaces_the_built_in_one()
    {
        var tuned = GtaKeyboardMousePreset.Profile with { ShowStats = true };

        var profiles = BuiltInProfiles.With([tuned]);

        Assert.Same(tuned, Assert.Single(profiles));
    }
}
