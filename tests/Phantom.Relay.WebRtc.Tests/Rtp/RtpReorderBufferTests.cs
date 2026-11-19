// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Testing.Allocations;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

public sealed class RtpReorderBufferTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(3);

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingPacketSink _sink = new(true);

    [Fact]
    public void Packets_in_order_go_through_at_once_and_nothing_waits()
    {
        var buffer = Buffer(8);

        Push(buffer, 10, 11, 12);

        Assert.Equal([10L, 11, 12], _sink.Sequences);
        Assert.Equal(0, buffer.Held);
    }

    [Fact]
    public void A_reordered_packet_inside_the_window_is_put_back_in_place_and_not_reported_missing()
    {
        var buffer = Buffer(8);

        Push(buffer, 1, 3);
        _clock.AdvanceMilliseconds(1);
        Push(buffer, 2);

        Assert.Equal([1L, 2, 3], _sink.Sequences);
        Assert.Equal(0, buffer.TakeMissing(new ushort[8]));
        Assert.Equal(0, buffer.Lost);
    }

    [Fact]
    public void A_hole_is_reported_at_once_and_given_up_when_the_window_runs_out()
    {
        var buffer = Buffer(8);
        var missing = new ushort[8];

        Push(buffer, 1, 4, 5);

        Assert.Equal(2, buffer.TakeMissing(missing));
        Assert.Equal(new ushort[] { 2, 3 }, missing[..2]);
        Assert.Equal([1L], _sink.Sequences);

        _clock.AdvanceMilliseconds(2);
        buffer.Service();
        Assert.Equal(2, buffer.Held);

        _clock.AdvanceMilliseconds(1);
        buffer.Service();
        Assert.Equal([1L, 4, 5], _sink.Sequences);
        Assert.Equal(2, buffer.Lost);
    }

    [Fact]
    public void A_packet_after_its_hole_was_given_up_is_dropped_as_late()
    {
        var buffer = Buffer(8);
        Push(buffer, 1, 3);
        _clock.AdvanceMilliseconds(3);
        buffer.Service();

        Push(buffer, 2);

        Assert.Equal([1L, 3], _sink.Sequences);
        Assert.Equal(1, buffer.Late);
    }

    [Fact]
    public void A_packet_too_far_ahead_gives_up_the_oldest_holes_at_once()
    {
        var buffer = Buffer(4);

        Push(buffer, 1, 3, 9);

        Assert.Equal([1L, 3], _sink.Sequences);
        Assert.Equal(1, buffer.Held);
        Assert.Equal(3, buffer.Lost);
    }

    [Fact]
    public void A_duplicate_of_a_held_or_delivered_packet_is_dropped()
    {
        var buffer = Buffer(8);

        Push(buffer, 1, 3, 3, 1);

        Assert.Equal([1L], _sink.Sequences);
        Assert.Equal(2, buffer.Late);
    }

    [Fact]
    public void The_order_holds_across_the_sequence_wrap()
    {
        var buffer = Buffer(8);

        Push(buffer, 65534, 0, 65535, 1);

        Assert.Equal([65534L, 65535, 65536, 65537], _sink.Sequences);
    }

    [Fact]
    public void A_zero_window_never_waits()
    {
        var buffer = new RtpReorderBuffer(_sink, _clock, TimeSpan.Zero, 8);

        Push(buffer, 1, 3, 2);

        Assert.Equal([1L, 3], _sink.Sequences);
    }

    [Fact]
    public void In_order_and_reordered_packets_allocate_nothing()
    {
        var counter = new RecordingPacketSink(false);
        var buffer = new RtpReorderBuffer(counter, _clock, Window, 8);
        var first = new byte[1200];
        var second = new byte[1200];
        var missing = new ushort[8];
        ushort sequence = 0;

        var report = AllocationMeter.Measure(() =>
        {
            Write(second, (ushort)(sequence + 1));
            Write(first, sequence);
            buffer.Push(second);
            buffer.Push(first);
            buffer.TakeMissing(missing);
            sequence += 2;
        });

        Assert.Equal(0, report.BytesPerRun);
        Assert.Equal(0, buffer.Lost);
    }

    private RtpReorderBuffer Buffer(int capacity)
    {
        return new RtpReorderBuffer(_sink, _clock, Window, capacity);
    }

    private static void Push(RtpReorderBuffer buffer, params int[] sequences)
    {
        foreach (int sequence in sequences)
        {
            var packet = new byte[20];
            Write(packet, (ushort)sequence);
            buffer.Push(packet);
        }
    }

    private static void Write(byte[] packet, ushort sequence)
    {
        packet[0] = 0x80;
        packet[1] = 96;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), sequence);
    }
}
