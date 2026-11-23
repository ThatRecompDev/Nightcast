// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

public sealed class AudioPacketTapTests
{
    [Fact]
    public void The_sink_gets_every_packet_and_a_listener_those_while_attached()
    {
        var sink = new CountingSink();
        var listener = new CountingSink();
        var tap = new AudioPacketTap(sink);

        tap.Push([1], 1, 100);
        tap.Attach(listener);
        tap.Push([2], 2, 580);
        tap.Detach();
        tap.Push([3], 3, 1060);

        Assert.Equal([100u, 580u, 1060u], sink.Timestamps);
        Assert.Equal([580u], listener.Timestamps);
    }
}
