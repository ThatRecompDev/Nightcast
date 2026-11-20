// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

public sealed class AccessUnitTapTests
{
    [Fact]
    public void Units_reach_the_attached_listener_only_until_it_is_detached()
    {
        var tap = new AccessUnitTap();
        var listener = new CountingListener();
        tap.Offer([1], 10, key: false);
        tap.Attach(listener);
        tap.Offer([1, 2], 20, key: true);

        tap.Detach();
        tap.Offer([1, 2, 3], 30, key: false);

        Assert.Equal([20u], listener.Timestamps);
    }

    [Fact]
    public void A_keyframe_request_waits_until_the_network_thread_sent_it()
    {
        var tap = new AccessUnitTap();
        Assert.False(tap.KeyframeRequested);

        tap.RequestKeyframe();
        var waiting = tap.KeyframeRequested;
        tap.KeyframeAsked();

        Assert.True(waiting);
        Assert.False(tap.KeyframeRequested);
    }

    private sealed class CountingListener : IAccessUnitListener
    {
        public List<uint> Timestamps { get; } = [];

        public void Received(byte[] unit, uint rtpTimestamp, bool key)
        {
            Timestamps.Add(rtpTimestamp);
        }
    }
}
