// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class AudioPacketRingTests
{
    [Fact]
    public void Packets_come_out_in_order_with_their_timestamps()
    {
        var ring = new AudioPacketRing(4, 8);
        var buffer = new byte[8];

        Assert.True(ring.TryAdd([1, 2, 3], 100, 7));
        Assert.True(ring.TryAdd([4], 580, 8));

        Assert.Equal(3, ring.TryTake(buffer, out var timestamp, out var arrival));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer[..3]);
        Assert.Equal((100u, 7L), (timestamp, arrival));
        Assert.Equal(1, ring.TryTake(buffer, out timestamp, out _));
        Assert.Equal(580u, timestamp);
        Assert.Equal(-1, ring.TryTake(buffer, out _, out _));
    }

    [Fact]
    public void A_full_ring_or_a_packet_too_large_is_dropped_and_counted()
    {
        var ring = new AudioPacketRing(1, 2);

        Assert.False(ring.TryAdd([1, 2, 3], 0, 0));
        Assert.True(ring.TryAdd([1], 0, 0));
        Assert.False(ring.TryAdd([2], 0, 0));
        Assert.Equal(2, ring.Dropped);
    }
}
