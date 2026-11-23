// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

public sealed class PictureGrabTests
{
    [Fact]
    public async Task A_request_is_answered_by_the_picture_completed()
    {
        var grab = new PictureGrab();
        var request = grab.Request()!;
        var picture = new VideoFrame();

        Assert.True(grab.IsWanted);
        grab.Complete(picture);

        Assert.Same(picture, await request);
        Assert.False(grab.IsWanted);
    }

    [Fact]
    public void Only_one_request_waits_at_a_time()
    {
        var grab = new PictureGrab();

        Assert.NotNull(grab.Request());
        Assert.Null(grab.Request());
    }

    [Fact]
    public void A_cancelled_request_ends_and_a_new_one_may_be_made()
    {
        var grab = new PictureGrab();
        var request = grab.Request()!;

        grab.Cancel();

        Assert.True(request.IsCanceled);
        Assert.NotNull(grab.Request());
    }

    [Fact]
    public void A_copy_has_the_pictures_planes_and_colour()
    {
        var source = new VideoFrame();
        source.Prepare(4, 2, 2, 1, alpha: false);
        source.Luma.Fill(50);
        source.Cb.Fill(60);
        source.Cr.Fill(70);
        source.Colour = new VideoColour(VideoColourSpace.Bt601, VideoRange.Full);

        var copy = new VideoFrame();
        PictureGrab.Copy(source, copy);

        Assert.Equal((4, 2, 2, 1), (copy.Width, copy.Height, copy.ChromaWidth, copy.ChromaHeight));
        Assert.Equal(source.Luma.ToArray(), copy.Luma.ToArray());
        Assert.Equal(source.Cr.ToArray(), copy.Cr.ToArray());
        Assert.Equal(source.Colour, copy.Colour);
    }
}
