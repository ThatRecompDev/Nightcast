// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Content.Storage;
using Phantom.Relay.Xbox.Input;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Profiles.Tests;

public sealed class ProfileTests
{
    private static readonly StreamProfile Gta = StreamProfile.Default with
    {
        Name = "GTA VI",
        TitleIds = ["1A2B3C4D"],
        Stream = new StreamTier(StreamResolution.P1080High),
        FrameGeneration = new FrameGeneration(FrameGenerationMode.Quality, FrameGeneration.Doubled, 60),
        Pad = PadSettings.Standard with
        {
            Curve = ResponseCurve.Smooth,
            Remap = new Dictionary<XboxButtons, XboxButtons> { [XboxButtons.A] = XboxButtons.B },
        },
    };

    [Fact]
    public void A_profile_survives_the_store_with_every_setting()
    {
        var store = new ProfileStore(new MemoryPersistentStore());

        Assert.True(store.Save(Gta));
        var loaded = Assert.Single(store.LoadAll());

        Assert.Equal("GTA VI", loaded.Name);
        Assert.Equal(["1A2B3C4D"], loaded.TitleIds);
        Assert.Equal(StreamResolution.P1080High, loaded.Stream.Resolution);
        Assert.Equal(new FrameGeneration(FrameGenerationMode.Quality, 2, 60), loaded.FrameGeneration);
        Assert.Equal(ResponseCurve.Smooth, loaded.Pad.Curve);
        Assert.Equal(XboxButtons.B, loaded.Pad.Remap[XboxButtons.A]);
    }

    [Fact]
    public void Profiles_are_kept_as_readable_json_under_a_safe_key()
    {
        var memory = new MemoryPersistentStore();

        new ProfileStore(memory).Save(Gta);
        string json = Encoding.UTF8.GetString(memory.Read("profile.GTA_VI")!);

        Assert.Contains("\"mode\": \"Quality\"", json, StringComparison.Ordinal);
        Assert.Contains("\"resolution\": \"P1080High\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Other_keys_in_the_store_are_not_profiles()
    {
        var memory = new MemoryPersistentStore();
        memory.Write("account.credentials", "{}"u8);

        var profiles = new ProfileStore(memory).LoadAll();

        Assert.Empty(profiles);
    }

    [Fact]
    public void The_title_picks_its_profile_and_anything_else_the_default()
    {
        var custom = StreamProfile.Default with { Name = "Mine", ShowStats = true };
        StreamProfile[] profiles = [Gta, custom];

        Assert.Same(Gta, ProfileChoice.For(profiles, "1a2b3c4d"));
        Assert.Same(custom, ProfileChoice.For(profiles, "FFFF0000"));
        Assert.Same(custom, ProfileChoice.For(profiles, null));
        Assert.Same(StreamProfile.Default, ProfileChoice.For([Gta], "FFFF0000"));
    }

    [Fact]
    public void The_default_profile_asks_for_the_highest_tier()
    {
        var settings = new StreamSettings(StreamProfile.Default.Stream.Resolution, "en-US", 0);

        Assert.Equal(StreamResolution.P1440, settings.Resolution);
        Assert.Equal("tizen", settings.OsName);
        Assert.Equal(FrameGenerationMode.Off, StreamProfile.Default.FrameGeneration.Mode);
    }

    [Fact]
    public void Deleting_a_profile_removes_its_key()
    {
        var store = new ProfileStore(new MemoryPersistentStore());
        store.Save(Gta);

        store.Delete("GTA VI");

        Assert.Empty(store.LoadAll());
    }
}
