// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.WebRtc.H264;

namespace Phantom.Relay.WebRtc.Tests.H264;

/// <summary>RFC 6184 packetization mode 1, rebuilt into Annex-B access units, with every way a unit can break.</summary>
public sealed class H264DepacketizerTests
{
    private const string StartCode = "00000001";

    /// <summary>An SPS (type 7) and a PPS (type 8) in one STAP-A.</summary>
    private static readonly byte[] ParameterSets = Convert.FromHexString("78" + "0004" + "674D002A" + "0002" + "68EE");

    /// <summary>An IDR slice (NRI 3, type 5) in three FU-A fragments: start, middle, end.</summary>
    private static readonly byte[] IdrStart = Convert.FromHexString("7C85" + "AAAA");
    private static readonly byte[] IdrMiddle = Convert.FromHexString("7C05" + "BBBB");
    private static readonly byte[] IdrEnd = Convert.FromHexString("7C45" + "CC");

    /// <summary>A non-IDR slice (NRI 2, type 1) as a single NAL unit packet.</summary>
    private static readonly byte[] Slice = Convert.FromHexString("41" + "9A0102");

    private readonly RecordingAccessUnitSink _sink = new(true);

    [Fact]
    public void A_keyframe_from_a_stap_a_and_three_fragments_becomes_one_annex_b_unit()
    {
        var depacketizer = Depacketizer();

        Keyframe(depacketizer, 0, 3000);

        var unit = Assert.Single(_sink.Units);
        Assert.Equal(StartCode + "674D002A" + StartCode + "68EE" + StartCode + "65" + "AAAABBBBCC", unit.Hex);
        Assert.Equal(3000u, unit.Timestamp);
        Assert.Equal(H264AccessUnitContent.Idr | H264AccessUnitContent.Sps | H264AccessUnitContent.Pps, unit.Content);
        Assert.False(depacketizer.NeedsKeyframe);
    }

    [Fact]
    public void Slices_before_the_first_keyframe_are_dropped_and_after_it_go_through()
    {
        var depacketizer = Depacketizer();

        depacketizer.Push(Slice, 0, 1500, true);
        Keyframe(depacketizer, 1, 3000);
        depacketizer.Push(Slice, 5, 4500, true);

        Assert.Equal(2, _sink.Units.Count);
        Assert.Equal(StartCode + "419A0102", _sink.Units[1].Hex);
        Assert.Equal(1, depacketizer.Dropped);
    }

    [Fact]
    public void A_unit_whose_marker_is_missing_completes_when_the_next_timestamp_arrives()
    {
        var depacketizer = Depacketizer();
        Keyframe(depacketizer, 0, 3000);

        depacketizer.Push(Slice, 4, 4500, false);
        depacketizer.Push(Slice, 5, 6000, true);

        Assert.Equal([3000u, 4500, 6000], _sink.Units.Select(unit => unit.Timestamp));
    }

    [Fact]
    public void A_gap_inside_a_keyframe_drops_it_and_slices_wait_for_the_next_keyframe()
    {
        var depacketizer = Depacketizer();

        depacketizer.Push(ParameterSets, 0, 3000, false);
        depacketizer.Push(IdrStart, 1, 3000, false);
        depacketizer.Push(IdrEnd, 3, 3000, true);
        depacketizer.Push(Slice, 4, 4500, true);

        Assert.Empty(_sink.Units);
        Assert.True(depacketizer.NeedsKeyframe);
        Assert.Equal(1, depacketizer.Gaps);

        Keyframe(depacketizer, 5, 6000);
        Assert.Single(_sink.Units);
        Assert.False(depacketizer.NeedsKeyframe);
    }

    [Fact]
    public void A_gap_between_units_breaks_the_next_one_too()
    {
        var depacketizer = Depacketizer();
        Keyframe(depacketizer, 0, 3000);

        depacketizer.Push(Slice, 5, 4500, true);
        depacketizer.Push(Slice, 6, 6000, true);

        Assert.Single(_sink.Units);
        Assert.True(depacketizer.NeedsKeyframe);
        Assert.Equal(2, depacketizer.Dropped);
    }

    [Fact]
    public void An_unfinished_fragment_breaks_its_unit()
    {
        var depacketizer = Depacketizer();
        Keyframe(depacketizer, 0, 3000);

        depacketizer.Push(IdrStart, 4, 4500, false);
        depacketizer.Push(Slice, 5, 4500, true);

        Assert.Single(_sink.Units);
        Assert.True(depacketizer.NeedsKeyframe);
    }

    [Fact]
    public void A_fragment_without_its_start_breaks_its_unit()
    {
        var depacketizer = Depacketizer();
        Keyframe(depacketizer, 0, 3000);

        depacketizer.Push(IdrMiddle, 4, 4500, false);
        depacketizer.Push(IdrEnd, 5, 4500, true);

        Assert.Single(_sink.Units);
        Assert.Equal(1, depacketizer.Dropped);
    }

    [Fact]
    public void A_unit_larger_than_the_buffer_is_dropped_not_cut()
    {
        var depacketizer = new H264Depacketizer(_sink, 16);

        Keyframe(depacketizer, 0, 3000);

        Assert.Empty(_sink.Units);
        Assert.True(depacketizer.NeedsKeyframe);
    }

    [Fact]
    public void Malformed_aggregates_and_reserved_types_break_their_unit()
    {
        var depacketizer = Depacketizer();
        Keyframe(depacketizer, 0, 3000);

        depacketizer.Push(Convert.FromHexString("78" + "0009" + "674D"), 4, 4500, true);
        Keyframe(depacketizer, 5, 6000);
        depacketizer.Push(Convert.FromHexString("7E" + "0000"), 9, 7500, true);

        Assert.Equal(2, _sink.Units.Count);
        Assert.Equal(2, depacketizer.Dropped);
    }

    [Fact]
    public void Rtp_packets_are_read_for_their_payload_timestamp_and_marker()
    {
        var depacketizer = Depacketizer();

        depacketizer.Deliver(Packet(ParameterSets, 7, 9000, false), 7);
        depacketizer.Deliver(Packet(Convert.FromHexString("65" + "0102"), 8, 9000, true), 8);

        var unit = Assert.Single(_sink.Units);
        Assert.Equal(StartCode + "674D002A" + StartCode + "68EE" + StartCode + "650102", unit.Hex);
    }

    [Fact]
    public void A_1440p_buffer_holds_a_raw_picture()
    {
        Assert.Equal(2560 * 1440 * 3 / 2, H264Depacketizer.CapacityFor(2560, 1440));
    }

    [Fact]
    public void Depacketizing_a_steady_stream_allocates_nothing()
    {
        var counter = new RecordingAccessUnitSink(false);
        var depacketizer = new H264Depacketizer(counter, 4096);
        var large = new byte[1100];
        large[0] = 0x7C;
        long sequence = 0;
        uint timestamp = 0;

        var report = AllocationMeter.Measure(() =>
        {
            Keyframe(depacketizer, sequence, timestamp);

            large[1] = 0x81;
            depacketizer.Push(large, sequence + 4, timestamp + 1500, false);
            large[1] = 0x41;
            depacketizer.Push(large, sequence + 5, timestamp + 1500, true);

            sequence += 6;
            timestamp += 3000;
        });

        Assert.Equal(0, report.BytesPerRun);
        Assert.Equal(0, depacketizer.Dropped);
    }

    private H264Depacketizer Depacketizer()
    {
        return new H264Depacketizer(_sink, 4096);
    }

    private static void Keyframe(H264Depacketizer depacketizer, long sequence, uint timestamp)
    {
        depacketizer.Push(ParameterSets, sequence, timestamp, false);
        depacketizer.Push(IdrStart, sequence + 1, timestamp, false);
        depacketizer.Push(IdrMiddle, sequence + 2, timestamp, false);
        depacketizer.Push(IdrEnd, sequence + 3, timestamp, true);
    }

    private static byte[] Packet(byte[] payload, ushort sequence, uint timestamp, bool marker)
    {
        var packet = new byte[12 + payload.Length];
        packet[0] = 0x80;
        packet[1] = 0x66;

        if (marker)
        {
            packet[1] |= 0x80;
        }

        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), sequence);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), timestamp);
        payload.CopyTo(packet, 12);

        return packet;
    }
}
