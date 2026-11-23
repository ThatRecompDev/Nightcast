// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Pictures;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.App.Tests.Pictures;

public sealed class FrameGenerationSwitchTests
{
    private const double ThirtyFps = 30;

    [Fact]
    public void F4_cycles_from_the_profiles_LSFG_to_NVIDIA_FRUC_to_off_and_back_to_LSFG()
    {
        var choice = new FrameGenerationSwitch();

        Assert.Same(FrameGeneration.QualityDoubled, choice.Follow(FrameGeneration.QualityDoubled));
        Assert.False(choice.Changed);
        Assert.Equal(FrameGeneration.QualityDoubled with { Generator = FrameGenerator.NvidiaFruc }, choice.Cycle());
        Assert.Equal("NVIDIA FRUC x2", choice.Describe(0));

        Assert.Same(FrameGeneration.Off, choice.Cycle());
        Assert.Equal("off", choice.Describe(ThirtyFps));
        Assert.Equal(FrameGeneration.QualityDoubled, choice.Cycle());
        Assert.Equal("LSFG x2 (quality, flow 1.00)", choice.Describe(0));
        Assert.True(choice.Changed);
    }

    [Fact]
    public void F5_steps_through_quality_performance_and_their_half_flows_then_back()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.QualityDoubled);

        Assert.Equal("LSFG x2 (performance, flow 1.00)", Describe(choice.Next(), choice));
        Assert.Equal("LSFG x2 (quality, flow 0.50)", Describe(choice.Next(), choice));
        Assert.Equal("LSFG x2 (performance, flow 0.50)", Describe(choice.Next(), choice));
        Assert.Equal("LSFG x2 (quality, flow 1.00)", Describe(choice.Next(), choice));
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
        Assert.Equal("NVIDIA FRUC x3 → 90 fps", choice.Describe(ThirtyFps));
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
    public void F8_steps_x2_x3_x4_and_back_for_the_generator_that_runs()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.QualityDoubled);

        Assert.Equal(3, choice.NextMultiplier().Multiplier);
        Assert.Equal("LSFG x3 → 90 fps (quality, flow 1.00)", choice.Describe(ThirtyFps));
        Assert.Equal(4, choice.NextMultiplier().Multiplier);
        Assert.Equal("LSFG x4 → 120 fps (quality, flow 1.00)", choice.Describe(ThirtyFps));

        choice.Cycle();
        Assert.Equal("NVIDIA FRUC x4 → 120 fps", choice.Describe(ThirtyFps));
        Assert.Equal((FrameGenerator.NvidiaFruc, 2), (choice.NextMultiplier().Generator, choice.Current.Multiplier));
    }

    [Fact]
    public void F8s_multiplier_holds_for_the_session_across_profiles_and_F8_turns_frame_generation_on()
    {
        var choice = new FrameGenerationSwitch();
        choice.Follow(FrameGeneration.Off);

        Assert.Equal((true, 3), (choice.NextMultiplier().IsOn, choice.Current.Multiplier));
        Assert.Equal(3, choice.Follow(FrameGeneration.QualityDoubled).Multiplier);
        Assert.Equal(3, choice.Follow(FrameGeneration.QualityDoubled with { Multiplier = 4 }).Multiplier);
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

    [Fact]
    public void An_external_generators_kept_choice_resumes_with_its_setting_and_multiplier()
    {
        var choice = new FrameGenerationSwitch();
        var kept = FrameGeneration.QualityDoubled with { Mode = FrameGenerationMode.Performance, Multiplier = 3, FlowScale = FrameGenerationSwitch.HalfFlow };

        Assert.Equal(kept, choice.Resume(kept));
        Assert.False(choice.Changed);
        Assert.Equal("LSFG x3 → 90 fps (performance, flow 0.50)", choice.Describe(ThirtyFps));
        Assert.Equal(3, choice.Follow(FrameGeneration.QualityDoubled).Multiplier);
        Assert.Same(FrameGeneration.Off, choice.Resume(FrameGeneration.Off));
    }

    [Fact]
    public void F4_for_an_external_generator_turns_it_off_and_on_with_the_setting_chosen_last_never_FRUC()
    {
        var choice = new FrameGenerationSwitch();
        choice.Resume(FrameGeneration.QualityDoubled with { Generator = FrameGenerator.NvidiaFruc, Multiplier = 4 });

        Assert.Equal(FrameGenerator.Lsfg, choice.Current.Generator);
        Assert.Same(FrameGeneration.Off, choice.Toggle());
        Assert.Equal(FrameGeneration.QualityDoubled with { Multiplier = 4 }, choice.Toggle());
        Assert.True(choice.Changed);
    }

    private static string Describe(FrameGeneration settings, FrameGenerationSwitch choice)
    {
        Assert.True(settings.IsOn);

        return choice.Describe(0);
    }
}
