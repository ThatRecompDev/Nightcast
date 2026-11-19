// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Tests;

public sealed class LsfgSettingsTests
{
    [Fact]
    public void Frame_generation_is_off_by_default_at_x2_full_flow_scale_and_quality()
    {
        var settings = LsfgSettings.Default;

        Assert.False(settings.Enabled);
        Assert.Equal(2, settings.Multiplier);
        Assert.Equal(1f, settings.FlowScale);
        Assert.False(settings.PerformanceMode);
        Assert.Null(settings.LibraryPath);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 4)]
    public void The_multiplier_is_brought_into_2_to_4(int asked, int expected)
    {
        Assert.Equal(expected, (LsfgSettings.Default with { Multiplier = asked }).Normalized().Multiplier);
    }

    [Theory]
    [InlineData(0.1f, 0.25f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(2f, 1f)]
    [InlineData(float.NaN, 1f)]
    public void The_flow_scale_is_brought_into_a_quarter_to_one(float asked, float expected)
    {
        Assert.Equal(expected, (LsfgSettings.Default with { FlowScale = asked }).Normalized().FlowScale);
    }
}
