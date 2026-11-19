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
    public void F6_steps_through_every_upscaler_and_the_old_sharp_name_is_fsr()
    {
        UpscaleMethod[] order = [UpscaleMethod.Fsr, UpscaleMethod.Ls1, UpscaleMethod.Ls1Performance, UpscaleMethod.Sgsr, UpscaleMethod.Xbr, UpscaleMethod.Bilinear, UpscaleMethod.Fsr];

        for (var step = 0; step < order.Length - 1; step++)
        {
            Assert.Equal(order[step + 1], StreamUpscaler.Next(order[step]));
        }

        var upscaler = Initialized();
        upscaler.Use(UpscaleMethod.Sharp);
        Assert.Equal(UpscaleMethod.Fsr, upscaler.Method);
    }

    [Fact]
    public void F7_steps_rcas_sharpness_sharper_then_softer_and_back()
    {
        Assert.Equal(0.1f, StreamUpscaler.NextSharpness(0.2f));
        Assert.Equal(0f, StreamUpscaler.NextSharpness(0.1f));
        Assert.Equal(0.5f, StreamUpscaler.NextSharpness(0f));
        Assert.Equal(0.2f, StreamUpscaler.NextSharpness(0.5f));

        var upscaler = Initialized();
        Assert.Equal(0.2f, upscaler.Sharpness);
        upscaler.UseSharpness(0f);
        Assert.Equal(0f, upscaler.Sharpness);
    }

    [Fact]
    public void A_decoder_side_method_falls_back_to_fsr_for_a_picture_not_yet_upscaled_and_draws_one_already_upscaled_as_it_is()
    {
        var upscaler = Initialized();
        upscaler.Use(UpscaleMethod.Ls1);

        Assert.Equal(UpscaleMethod.Fsr, upscaler.EngineMethod);
        Assert.True(upscaler.Plan(2560, 1440, Whole, 3840, 2160));
        Assert.False(upscaler.Plan(3840, 2160, Whole, 3840, 2160));
    }

    private static StreamUpscaler Initialized()
    {
        var upscaler = new StreamUpscaler(new RecordingGraphicsDevice(RecordingGraphicsDevice.WebGpu));
        upscaler.Initialize();

        return upscaler;
    }
}
