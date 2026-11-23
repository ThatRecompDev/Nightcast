// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Interop;

namespace Phantom.Relay.Pads.Linux.Tests.Interop;

/// <summary>The requests against the values <c>linux/input.h</c>'s macros give on x86-64, worked out by hand.</summary>
public sealed class EvdevRequestsTests
{
    [Fact]
    public void The_fixed_requests_match_the_kernel_headers()
    {
        Assert.Equal(0x80084502u, EvdevRequests.Identity);
        Assert.Equal(0x40304580u, EvdevRequests.Upload);
        Assert.Equal(0x40044581u, EvdevRequests.Remove);
    }

    [Fact]
    public void The_sized_requests_match_the_kernel_headers()
    {
        Assert.Equal(0x80604521u, EvdevRequests.Bits(EvdevCodes.Key, 96));
        Assert.Equal(0x80084523u, EvdevRequests.Bits(EvdevCodes.Absolute, 8));
        Assert.Equal(0x80104535u, EvdevRequests.Bits(EvdevCodes.ForceFeedback, 16));
        Assert.Equal(0x80604518u, EvdevRequests.Keys(96));
        Assert.Equal(0x80184540u, EvdevRequests.Axis(EvdevCodes.AxisX));
        Assert.Equal(0x80184551u, EvdevRequests.Axis(EvdevCodes.AxisHatY));
    }

    [Fact]
    public void A_rumble_effect_carries_its_type_id_and_magnitudes_at_their_offsets()
    {
        var effect = Enumerable.Repeat((byte)0x55, 48).ToArray();

        RumbleEffect.Write(effect, RumbleEffect.NewEffect, 0xFFFF, 0x8000);

        Assert.Equal(new byte[] { 0x50, 0x00, 0xFF, 0xFF }, effect[..4]);
        Assert.All(effect[4..16], static value => Assert.Equal(0, value));
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0x00, 0x80 }, effect[16..20]);
        Assert.All(effect[20..], static value => Assert.Equal(0, value));
        Assert.Equal(-1, RumbleEffect.Id(effect));
    }

    [Fact]
    public void Playback_plays_and_stops_the_effect_by_its_id()
    {
        Assert.Equal(new InputEvent(EvdevCodes.ForceFeedback, 3, 1), RumbleEffect.Playback(3, play: true));
        Assert.Equal(new InputEvent(EvdevCodes.ForceFeedback, 3, 0), RumbleEffect.Playback(3, play: false));
    }
}
