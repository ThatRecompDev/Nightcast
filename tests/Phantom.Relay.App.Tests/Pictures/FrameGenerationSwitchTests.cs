// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Pictures;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.App.Tests.Pictures;

public sealed class FrameGenerationSwitchTests
{
    [Fact]
    public void F4_cycles_from_the_profiles_LSFG_to_NVIDIA_FRUC_to_off_and_back_to_LSFG()
    {
        var choice = new FrameGenerationSwitch();

        Assert.Same(FrameGeneration.QualityDoubled, choice.Follow(FrameGeneration.QualityDoubled));
        Assert.False(choice.Changed);
        Assert.Equal(FrameGeneration.QualityDoubled with { Generator = FrameGenerator.NvidiaFruc }, choice.Cycle());
        Assert.Equal("NVIDIA FRUC x2", choice.Describe());

        Assert.Same(FrameGeneration.Off, choice.Cycle());
        Assert.Equal("off", choice.Describe());
        Assert.Equal(FrameGeneration.QualityDoubled, choice.Cycle());
        Assert.Equal("LSFG x2 quality, flow 1.00", choice.Describe());
        Assert.True(choice.Changed);
    }

    [Fact]
    public void F5_steps_through_quality_performance_and_their_half_flows_then_back()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.QualityDoubled);

        Assert.Equal("LSFG x2 performance, flow 1.00", Describe(choice.Next(), choice));
        Assert.Equal("LSFG x2 quality, flow 0.50", Describe(choice.Next(), choice));
        Assert.Equal("LSFG x2 performance, flow 0.50", Describe(choice.Next(), choice));
        Assert.Equal("LSFG x2 quality, flow 1.00", Describe(choice.Next(), choice));
    }

    [Fact]
    public void F4_in_a_game_without_frame_generation_turns_on_LSFG_quality_x2()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.Off);

        Assert.Equal(FrameGeneration.QualityDoubled, choice.Cycle());
    }

    [Fact]
    public void A_profile_with_NVIDIA_FRUC_plays_it_and_F4_turns_it_off_then_to_LSFG()
    {
        var choice = new FrameGenerationSwitch();
        var fruc = FrameGeneration.QualityDoubled with { Generator = FrameGenerator.NvidiaFruc, Multiplier = 3 };

        Assert.Same(fruc, choice.Follow(fruc));
        Assert.Equal("NVIDIA FRUC x3", choice.Describe());
        Assert.Same(FrameGeneration.Off, choice.Cycle());
        Assert.Equal((FrameGenerator.Lsfg, 3), (choice.Cycle().Generator, choice.Current.Multiplier));
    }

    [Fact]
    public void F5_keeps_the_profiles_multiplier_and_turns_LSFG_on()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.QualityDoubled with { Multiplier = 3, Generator = FrameGenerator.NvidiaFruc });
        choice.Cycle();

        var next = choice.Next();

        Assert.Equal((FrameGenerationMode.Performance, 3, FrameGenerator.Lsfg), (next.Mode, next.Multiplier, next.Generator));
        Assert.True(next.IsOn);
    }

    [Fact]
    public void A_new_profile_starts_from_its_own_setting()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.QualityDoubled);
        choice.Next();

        Assert.Same(FrameGeneration.Off, choice.Follow(FrameGeneration.Off));
        Assert.False(choice.Changed);
    }

    private static string Describe(FrameGeneration settings, FrameGenerationSwitch choice)
    {
        Assert.True(settings.IsOn);

        return choice.Describe();
    }
}
