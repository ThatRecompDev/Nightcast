// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Testing.Graphics;
using Phantom.Relay.App.Pictures;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.App.Tests.Pictures;

public sealed class StreamUpscalerTests
{
    private static readonly Vector4 Whole = new(0f, 0f, 1f, 1f);

    [Fact]
    public void A_1440p_picture_in_a_4k_window_is_upscaled_with_fsr_by_default()
    {
        var upscaler = Initialized();

        Assert.Equal(UpscaleMethod.Fsr, upscaler.Method);
        Assert.True(upscaler.Plan(2560, 1440, Whole, 3840, 2160));
        Assert.Equal((3840, 2160), (upscaler.OutputWidth, upscaler.OutputHeight));
        Assert.True(upscaler.IsStale);
    }

    [Fact]
    public void A_window_no_larger_than_the_picture_or_upscaling_off_draws_it_as_before()
    {
        var upscaler = Initialized();

        Assert.False(upscaler.Plan(2560, 1440, Whole, 1280, 720));
        upscaler.Use(UpscaleMethod.Bilinear);
        Assert.False(upscaler.Plan(2560, 1440, Whole, 3840, 2160));
    }

    [Fact]
    public void The_targets_are_made_once_for_a_size_and_again_when_it_changes()
    {
        var upscaler = Initialized();
        upscaler.Plan(2560, 1440, Whole, 3840, 2160);
        upscaler.BeginPicture();
        upscaler.EndPicture();

        Assert.True(upscaler.Plan(2560, 1440, Whole, 3840, 2160));
        Assert.False(upscaler.IsStale);
        Assert.True(upscaler.Plan(2560, 1440, Whole, 3200, 1800));
        Assert.True(upscaler.IsStale);
    }

    [Fact]
    public void F6_steps_through_fsr_off_and_xbr_and_the_old_sharp_name_is_fsr()
    {
        Assert.Equal(UpscaleMethod.Bilinear, StreamUpscaler.Next(UpscaleMethod.Fsr));
        Assert.Equal(UpscaleMethod.Xbr, StreamUpscaler.Next(UpscaleMethod.Bilinear));
        Assert.Equal(UpscaleMethod.Fsr, StreamUpscaler.Next(UpscaleMethod.Xbr));

        var upscaler = Initialized();
        upscaler.Use(UpscaleMethod.Sharp);
        Assert.Equal(UpscaleMethod.Fsr, upscaler.Method);
    }

    private static StreamUpscaler Initialized()
    {
        var upscaler = new StreamUpscaler(new RecordingGraphicsDevice(RecordingGraphicsDevice.WebGpu));
        upscaler.Initialize();

        return upscaler;
    }
}
