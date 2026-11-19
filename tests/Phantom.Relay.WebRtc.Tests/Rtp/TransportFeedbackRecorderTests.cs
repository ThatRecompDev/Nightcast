// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

/// <summary>
/// Transport-wide feedback (draft-holmer-rmcat-transport-wide-cc-extensions-01 section 3.1), checked octet by octet
/// against packets worked out by hand: header "8FCD" + length, sender, media source, base sequence and status count,
/// reference time (64 ms units) and feedback count, the chunks, the deltas (250 µs units), the zero padding.
/// </summary>
public sealed class TransportFeedbackRecorderTests
{
    private const uint Me = 0x01020304;
    private const uint Console = 0xA0B0C0D0;

    private readonly TransportFeedbackRecorder _recorder = new(1024);

    [Fact]
    public void Three_packets_go_in_a_one_bit_vector_with_small_deltas_from_the_reference_time()
    {
        // 129 ms is 516 units of 250 µs; the reference time is 2 (128 ms), so the first delta is 4.
        Arrive(10, 129);
        Arrive(11, 130);
        Arrive(12, 130.25);

        Assert.Equal(
            "8FCD0006" + "01020304" + "A0B0C0D0" + "000A0003" + "00000200" + "B800" + "040401" + "000000",
            Write());
    }

    [Fact]
    public void Fourteen_or_more_alike_go_in_a_run_length_chunk()
    {
        for (int i = 0; i < 20; i++)
        {
            Arrive((ushort)(100 + i), i);
        }

        Assert.Equal(
            "8FCD000A" + "01020304" + "A0B0C0D0" + "00640014" + "00000000" + "2014" + "00" + string.Concat(Enumerable.Repeat("04", 19)) + "0000",
            Write());
    }

    [Fact]
    public void A_long_run_of_losses_is_a_run_length_chunk_between_one_bit_vectors()
    {
        Arrive(1, 0);
        Arrive(40, 1);

        // Statuses 1, 38 losses, 1: a vector of 14 (1 then 13 losses), a run of 25 losses, a vector of the last.
        Assert.Equal(
            "8FCD0006" + "01020304" + "A0B0C0D0" + "00010028" + "00000000" + "A000" + "0019" + "A000" + "0004",
            Write());
    }

    [Fact]
    public void Large_and_negative_deltas_and_a_loss_go_in_a_two_bit_vector()
    {
        // 4 arrives before 3: 3 is 100 ms after 1 (400 units, large), 4 is 1 ms before 3 (-4, large); 2 is lost.
        Arrive(1, 0);
        Arrive(4, 99);
        Arrive(3, 100);

        Assert.Equal(
            "8FCD0006" + "01020304" + "A0B0C0D0" + "00010004" + "00000000" + "D280" + "00" + "0190" + "FFFC" + "00",
            Write());
    }

    [Fact]
    public void The_sixteen_bit_sequence_wraps_inside_one_feedback()
    {
        Arrive(65534, 0);
        Arrive(65535, 1);
        Arrive(0, 2);
        Arrive(1, 3);

        Assert.Equal(
            "8FCD0006" + "01020304" + "A0B0C0D0" + "FFFE0004" + "00000000" + "BC00" + "00040404" + "0000",
            Write());
    }

    [Fact]
    public void A_delta_beyond_sixteen_bits_starts_the_next_feedback_with_its_own_reference_time()
    {
        // 9 s is 36000 units, past 32767: the second packet waits for the next feedback, whose reference time is 140
        // (8960 ms, 35840 units), so its delta is 160; the feedback count goes to 1.
        Arrive(1, 0);
        Arrive(2, 9000);

        Assert.Equal("8FCD0005" + "01020304" + "A0B0C0D0" + "00010001" + "00000000" + "A000" + "00" + "00", Write());
        Assert.True(_recorder.HasPending);
        Assert.Equal("8FCD0005" + "01020304" + "A0B0C0D0" + "00020001" + "00008C01" + "A000" + "A0" + "00", Write());
        Assert.Equal(2, _recorder.FeedbackCount);
    }

    [Fact]
    public void A_packet_reported_lost_is_not_reported_again_and_nothing_pending_writes_nothing()
    {
        Assert.Equal(0, _recorder.Write(new byte[64], Me, Console));
        Arrive(5, 0);
        Arrive(7, 1);

        Assert.Equal("8FCD0005" + "01020304" + "A0B0C0D0" + "00050003" + "00000000" + "A800" + "0004", Write());

        Arrive(6, 2);

        Assert.False(_recorder.HasPending);
        Assert.Equal(0, _recorder.Write(new byte[64], Me, Console));
    }

    [Fact]
    public void A_feedback_too_long_for_the_buffer_ends_early_and_the_rest_follows()
    {
        for (int i = 0; i < 100; i++)
        {
            Arrive((ushort)i, i * 100);
        }

        var buffer = new byte[64];
        int first = _recorder.Write(buffer, Me, Console);
        ushort count = (ushort)((buffer[14] << 8) | buffer[15]);

        Assert.True(first is > 0 and <= 64);
        Assert.True(count < 100);
        Assert.True(_recorder.HasPending);

        int second = _recorder.Write(buffer, Me, Console);

        Assert.True(second > 0);
        Assert.Equal(count, (buffer[12] << 8) | buffer[13]);
    }

    [Fact]
    public void The_capacity_is_a_power_of_two()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransportFeedbackRecorder(1000));
    }

    [Fact]
    public void Recording_and_writing_allocate_nothing()
    {
        var buffer = new byte[1200];
        ushort sequence = 0;
        long at = 0;

        var report = AllocationMeter.Measure(() =>
        {
            _recorder.Record(sequence++, TimeSpan.FromTicks(at));
            _recorder.Record(sequence++, TimeSpan.FromTicks(at + 30000));
            at += 1_000_000;
            _recorder.Write(buffer, Me, Console);
        });

        Assert.Equal(0, report.BytesPerRun);
    }

    private void Arrive(ushort sequence, double milliseconds)
    {
        _recorder.Record(sequence, TimeSpan.FromMilliseconds(milliseconds));
    }

    private string Write()
    {
        var buffer = new byte[1200];
        int length = _recorder.Write(buffer, Me, Console);

        return Convert.ToHexString(buffer.AsSpan(0, length));
    }
}
