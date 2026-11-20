// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Recording;
using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class CompareLabelsTests
{
    [Fact]
    public void Running_frame_generation_names_the_generator_and_its_multiplier()
    {
        var settings = new RecordingSettings("LSFG x3 (quality, flow 1.00) 0.8 ms", 3, "FSR 1");

        Assert.Equal("Relay LSFG x3", CompareLabels.Shown(settings));
        Assert.Equal(3, CompareLabels.Multiplier(settings));
    }

    [Fact]
    public void Frame_generation_chosen_but_off_counts_as_off()
    {
        var settings = new RecordingSettings("LSFG off: the pictures are read back", 2, "FSR 1");

        Assert.Equal(1, CompareLabels.Multiplier(settings));
        Assert.Equal("Relay without frame generation", CompareLabels.Shown(settings));
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 60)]
    [InlineData(3, 90)]
    [InlineData(4, 120)]
    public void The_shown_rate_is_the_consoles_or_the_games_times_the_multiplier(int multiplier, int rate)
    {
        Assert.Equal(rate, CompareLabels.ShownRate(multiplier));
    }

    [Fact]
    public void The_source_is_labelled_with_the_new_pictures_a_second()
    {
        Assert.Equal("console 30 fps", CompareLabels.Source(29.6));
        Assert.Equal("console", CompareLabels.Source(0));
    }
}
