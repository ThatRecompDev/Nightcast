// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

public sealed class Mp4VideoTrackTests
{
    [Fact]
    public void A_key_frame_gives_its_parameter_sets_without_start_codes()
    {
        var track = Mp4VideoTrack.FromKeyFrame(SyntheticMedia.KeyUnit(1), 1920, 1080);

        Assert.NotNull(track);
        Assert.Equal(SyntheticMedia.Sps, track.SequenceParameterSet.ToArray());
        Assert.Equal(SyntheticMedia.Pps, track.PictureParameterSet.ToArray());
        Assert.Equal((1920, 1080), (track.Width, track.Height));
    }

    [Fact]
    public void A_unit_without_parameter_sets_gives_none()
    {
        Assert.Null(Mp4VideoTrack.FromKeyFrame(SyntheticMedia.DeltaUnit(1), 1920, 1080));
    }
}
