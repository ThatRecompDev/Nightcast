// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Pictures;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Tests.Pictures;

public sealed class ScaleRequestTests
{
    [Fact]
    public void A_renderer_side_method_asks_for_nothing()
    {
        var request = new ScaleRequest();

        Assert.False(request.Update(UpscaleMethod.Fsr, 2, 3600, 2025, out _));
        Assert.Same(PictureScale.None, request.Current);
    }

    [Fact]
    public void A_decoder_side_method_is_asked_for_at_once_with_the_rectangle_and_then_not_again()
    {
        var request = new ScaleRequest();

        Assert.True(request.Update(UpscaleMethod.Ls1, 2, 3600, 2025, out var scale));
        Assert.Equal(new PictureScale(UpscaleMethod.Ls1, 3600, 2025), scale);
        Assert.False(request.Update(UpscaleMethod.Ls1, 2, 3600, 2025, out _));
    }

    [Fact]
    public void A_new_size_is_asked_for_only_once_it_has_held()
    {
        var request = new ScaleRequest();
        request.Update(UpscaleMethod.Ls1, 2, 3600, 2025, out _);

        // Dragging: every frame another size, none asked for.
        for (var frame = 0; frame < ScaleRequest.SettleFrames * 2; frame++)
        {
            Assert.False(request.Update(UpscaleMethod.Ls1, 2, 3000 + frame, 1700, out _));
        }

        var asked = Enumerable.Range(0, ScaleRequest.SettleFrames + 1).Count(frame => request.Update(UpscaleMethod.Ls1, 2, 3200, 1800, out _));

        Assert.Equal(1, asked);
        Assert.Equal(new PictureScale(UpscaleMethod.Ls1, 3200, 1800), request.Current);
    }

    [Fact]
    public void Another_method_goes_out_at_once_and_a_renderer_side_one_asks_for_nothing_again()
    {
        var request = new ScaleRequest();
        request.Update(UpscaleMethod.Ls1, 2, 3600, 2025, out _);

        Assert.True(request.Update(UpscaleMethod.Sgsr, 2, 3600, 2025, out var sgsr));
        Assert.Equal(UpscaleMethod.Sgsr, sgsr.Method);
        Assert.True(request.Update(UpscaleMethod.Xbr, 2, 3600, 2025, out var none));
        Assert.Same(PictureScale.None, none);
    }

    [Fact]
    public void Another_strength_goes_out_at_once_and_f7_steps_it_round()
    {
        var request = new ScaleRequest();
        request.Update(UpscaleMethod.Ls1, 2, 3600, 2025, out _);

        Assert.True(request.Update(UpscaleMethod.Ls1, 3, 3600, 2025, out var stronger));
        Assert.Equal(3, stronger.Strength);
        Assert.Equal((3, 4, 0, 1, 2), (PictureScale.NextStrength(2), PictureScale.NextStrength(3), PictureScale.NextStrength(4), PictureScale.NextStrength(0), PictureScale.NextStrength(1)));
    }
}
