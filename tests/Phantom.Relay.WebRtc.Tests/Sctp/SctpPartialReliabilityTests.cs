// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

public sealed class SctpPartialReliabilityTests
{
    private static readonly SctpSendPolicy NoRetransmits = new(false, SctpReliability.MaxRetransmits, 0);
    private static readonly SctpSendPolicy UnorderedFiftyMilliseconds = new(true, SctpReliability.Lifetime, 50);

    [Fact]
    public void Ordered_messages_without_retransmissions_are_skipped_and_the_stream_goes_on_in_order()
    {
        var pair = new SctpPair(21);
        pair.Connect();
        pair.ToServer.Blocked = true;
        pair.Client.Send(0, 53, [1], NoRetransmits);
        pair.Client.Send(0, 53, [2, 2, 2], NoRetransmits);
        pair.Run(2);

        pair.ToServer.Blocked = false;
        pair.Client.Send(0, 53, [3], SctpSendPolicy.Reliable);
        pair.Client.Send(0, 53, [4], SctpSendPolicy.Reliable);
        pair.Run(400);

        var received = SctpPair.Drain(pair.Server);
        Assert.Equal([3, 4], received.Select(static message => message.Payload[0]));
        Assert.Equal(0, pair.Client.BufferedAmount);
    }

    [Fact]
    public void A_fragmented_message_given_up_halfway_is_dropped_whole_at_the_receiver()
    {
        var pair = new SctpPair(22);
        pair.Connect();
        var large = new byte[5000];
        large.AsSpan().Fill(9);
        pair.ToServer.DropRate = 0.5;

        pair.Client.Send(0, 53, large, NoRetransmits);
        pair.Run(3);
        pair.ToServer.DropRate = 0;
        pair.Client.Send(0, 53, [5], SctpSendPolicy.Reliable);
        pair.Run(400);

        var received = SctpPair.Drain(pair.Server);
        Assert.Equal(5, received[^1].Payload[0]);
        Assert.All(received, static message => Assert.True(message.Payload.Length is 1 or 5000));
        Assert.Equal(0, pair.Client.BufferedAmount);
    }

    [Fact]
    public void Unordered_messages_past_their_lifetime_are_given_up_even_unsent()
    {
        var pair = new SctpPair(23);
        pair.Connect();
        pair.ToServer.Blocked = true;
        pair.Client.Send(1, 51, [1], UnorderedFiftyMilliseconds);
        pair.Client.Send(1, 51, [2], UnorderedFiftyMilliseconds);
        pair.Run(20);

        pair.ToServer.Blocked = false;
        pair.Client.Send(1, 51, [3], SctpSendPolicy.Reliable);
        pair.Run(400);

        var received = SctpPair.Drain(pair.Server);
        Assert.Equal([3], received.Select(static message => message.Payload[0]));
        Assert.Equal(0, pair.Client.BufferedAmount);
    }

    [Fact]
    public void Reliable_messages_are_never_given_up()
    {
        var pair = new SctpPair(24);
        pair.Connect();
        pair.ToServer.Blocked = true;
        pair.Client.Send(0, 53, [1], SctpSendPolicy.Reliable);
        pair.Run(200);

        pair.ToServer.Blocked = false;
        pair.Run(800);

        Assert.Equal([1], SctpPair.Drain(pair.Server).Select(static message => message.Payload[0]));
    }
}
