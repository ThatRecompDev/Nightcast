// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Collections;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.Udp;
using Phantom.Relay.Stream.Network;

namespace Phantom.Relay.Stream.Tests.Network;

public sealed class LoopbackDoorbellTests
{
    private const int MaxDatagram = 64;

    [Fact]
    public void Ringing_ends_the_streams_wait_with_one_byte_that_is_no_stun_dtls_or_rtp()
    {
        using var stream = new UdpDatagramTransport(DatagramEndpoint.FromOctets(127, 0, 0, 1, 0), MaxDatagram);
        using var bell = new UdpDatagramTransport(DatagramEndpoint.FromOctets(127, 0, 0, 1, 0), MaxDatagram);
        var doorbell = new LoopbackDoorbell(bell, stream.LocalEndpoint.Port);

        doorbell.Wake();
        bool woken = ((IDatagramWaiter)stream).WaitForDatagram(TimeSpan.FromSeconds(1));

        var ring = new ByteRing(ByteRing.CapacityFor(DatagramFrame.FrameLength(MaxDatagram)));
        stream.Receive(ring);
        Assert.True(woken);
        Assert.True(ring.TryPeek(out var frame));
        Assert.Equal([0], DatagramFrame.Payload(frame).ToArray());
    }

    [Fact]
    public void Without_a_ring_the_wait_times_out()
    {
        using var stream = new UdpDatagramTransport(DatagramEndpoint.FromOctets(127, 0, 0, 1, 0), MaxDatagram);

        Assert.False(((IDatagramWaiter)stream).WaitForDatagram(TimeSpan.FromMilliseconds(1)));
    }
}
