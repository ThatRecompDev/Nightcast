// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

public sealed class SctpDataTests
{
    private static readonly SctpSendPolicy Unordered = new(true, SctpReliability.Reliable, 0);

    [Fact]
    public void Ordered_messages_arrive_once_and_in_order_with_their_stream_and_protocol()
    {
        var pair = new SctpPair(11);
        pair.Connect();

        for (int index = 0; index < 100; index++)
        {
            Assert.True(pair.Client.Send(4, 51, Message(index, 20), SctpSendPolicy.Reliable));
        }

        pair.Run(50);

        var received = SctpPair.Drain(pair.Server);
        Assert.Equal(100, received.Count);
        Assert.All(received, static message => Assert.Equal((ushort)4, message.Stream));
        Assert.All(received, static message => Assert.Equal(51u, message.Protocol));
        Assert.Equal(Enumerable.Range(0, 100), received.Select(static message => IndexOf(message.Payload)));
    }

    [Fact]
    public void A_large_message_is_cut_into_chunks_and_put_back_together()
    {
        var pair = new SctpPair(12);
        pair.Connect();
        var large = Message(7, 20_000);

        pair.Client.Send(0, 53, large, SctpSendPolicy.Reliable);
        pair.Run(50);

        var received = SctpPair.Drain(pair.Server);
        Assert.Single(received);
        Assert.Equal(large, received[0].Payload);
    }

    [Fact]
    public void Both_sides_send_at_once()
    {
        var pair = new SctpPair(13);
        pair.Connect();

        pair.Client.Send(0, 51, Message(1, 10), SctpSendPolicy.Reliable);
        pair.Server.Send(1, 53, Message(2, 3000), SctpSendPolicy.Reliable);
        pair.Run(50);

        Assert.Equal(Message(1, 10), Assert.Single(SctpPair.Drain(pair.Server)).Payload);
        Assert.Equal(Message(2, 3000), Assert.Single(SctpPair.Drain(pair.Client)).Payload);
    }

    [Fact]
    public void Messages_survive_loss_duplication_and_reordering_in_order_and_exactly_once()
    {
        var pair = new SctpPair(14);
        pair.Connect();
        Degrade(pair.ToServer);
        Degrade(pair.ToClient);

        for (int index = 0; index < 300; index++)
        {
            Assert.True(pair.Client.Send((ushort)(index % 3 * 2), 53, Message(index, 1 + (index * 37 % 2500)), SctpSendPolicy.Reliable));
        }

        var received = RunUntil(pair, pair.Server, 300);

        Assert.Equal(300, received.Count);

        foreach (var stream in received.GroupBy(static message => message.Stream))
        {
            var indices = stream.Select(static message => IndexOf(message.Payload)).ToList();
            Assert.Equal(indices.Order(), indices);
        }

        Assert.All(received, static message => Assert.Equal(Message(IndexOf(message.Payload), message.Payload.Length), message.Payload));
    }

    [Fact]
    public void Unordered_messages_all_arrive_on_a_bad_link()
    {
        var pair = new SctpPair(15);
        pair.Connect();
        Degrade(pair.ToServer);
        Degrade(pair.ToClient);

        for (int index = 0; index < 200; index++)
        {
            pair.Client.Send(2, 53, Message(index, 1 + (index * 53 % 3000)), Unordered);
        }

        var received = RunUntil(pair, pair.Server, 200);

        Assert.Equal(Enumerable.Range(0, 200), received.Select(static message => IndexOf(message.Payload)).Order());
    }

    [Fact]
    public void Data_never_acknowledged_ends_the_association_after_its_retries()
    {
        var pair = new SctpPair(16);
        pair.Connect();
        pair.ToServer.Blocked = true;

        pair.Client.Send(0, 53, [1], SctpSendPolicy.Reliable);
        pair.Run(2000, 100);

        Assert.Equal(SctpCloseReason.TimedOut, pair.Client.CloseReason);
    }

    [Fact]
    public void A_full_send_buffer_refuses_messages_until_acknowledgements_free_it()
    {
        var options = SctpOptions.Default with { SendBufferLength = 4096 };
        var pair = SctpPair.WithOptions(options, 17);
        pair.Connect();

        bool first = pair.Client.Send(0, 53, Message(0, 3000), SctpSendPolicy.Reliable);
        bool second = pair.Client.Send(0, 53, Message(1, 3000), SctpSendPolicy.Reliable);
        pair.Run(100);
        bool afterAcks = pair.Client.Send(0, 53, Message(2, 3000), SctpSendPolicy.Reliable);

        Assert.True(first);
        Assert.False(second);
        Assert.True(afterAcks);
    }

    [Fact]
    public void Sending_and_receiving_a_message_allocates_nothing_once_warm()
    {
        var pair = new SctpPair(18);
        pair.Connect();
        var payload = Message(3, 64);

        void Step()
        {
            pair.Client.Send(0, 53, payload, SctpSendPolicy.Reliable);
            pair.Step(SctpPair.StepMilliseconds);

            while (pair.Server.TryPeek(out _, out _, out _))
            {
                pair.Server.Release();
            }
        }

        var report = AllocationMeter.Measure(Step);

        Assert.Equal(0, report.BytesPerRun);
    }

    private static void Degrade(FakeSctpLink link)
    {
        link.DropRate = 0.15;
        link.DuplicateRate = 0.1;
        link.ReorderRate = 0.3;
    }

    private static List<(ushort Stream, uint Protocol, byte[] Payload)> RunUntil(SctpPair pair, SctpAssociation receiver, int count)
    {
        var received = new List<(ushort Stream, uint Protocol, byte[] Payload)>();

        for (int step = 0; step < 20_000 && received.Count < count; step++)
        {
            pair.Step(SctpPair.StepMilliseconds);
            received.AddRange(SctpPair.Drain(receiver));
        }

        pair.Run(400);
        received.AddRange(SctpPair.Drain(receiver));

        return received;
    }

    /// <summary>A message whose first four bytes are its index and the rest a pattern of it.</summary>
    private static byte[] Message(int index, int length)
    {
        var message = new byte[Math.Max(length, 4)];
        BitConverter.TryWriteBytes(message, index);

        for (int at = 4; at < message.Length; at++)
        {
            message[at] = (byte)(index + at);
        }

        return message;
    }

    private static int IndexOf(byte[] payload)
    {
        return BitConverter.ToInt32(payload);
    }
}
