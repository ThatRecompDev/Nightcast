// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

/// <summary>NACK and PLI (RFC 4585) and receiver reports (RFC 3550), checked octet by octet against hand-made packets.</summary>
public sealed class RtcpWriterTests
{
    private const uint Me = 0x01020304;
    private const uint Console = 0xA0B0C0D0;

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_pli_is_the_twelve_octets_of_rfc_4585()
    {
        var buffer = new byte[64];

        int length = RtcpWriter.WritePictureLossIndication(buffer, Me, Console);

        Assert.Equal("81CE0002" + "01020304" + "A0B0C0D0", Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void A_nack_packs_nearby_losses_into_one_entry_and_starts_another_beyond_sixteen()
    {
        var buffer = new byte[64];
        ushort[] lost = [100, 101, 103, 116, 117, 117, 300];

        int length = RtcpWriter.WriteGenericNack(buffer, Me, Console, lost);

        // 100 with 101, 103 and 116 (bits 0, 2, 15); 117 with nothing; 300 with nothing.
        Assert.Equal(
            "81CD0005" + "01020304" + "A0B0C0D0" + "00648005" + "00750000" + "012C0000",
            Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void A_nack_spans_the_sequence_wrap()
    {
        var buffer = new byte[64];
        ushort[] lost = [65534, 65535, 0, 2];

        int length = RtcpWriter.WriteGenericNack(buffer, Me, Console, lost);

        Assert.Equal("81CD0003" + "01020304" + "A0B0C0D0" + "FFFE000B", Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void Nothing_lost_writes_nothing_and_a_short_buffer_is_refused()
    {
        Assert.Equal(0, RtcpWriter.WriteGenericNack(new byte[64], Me, Console, []));
        Assert.Throws<ArgumentException>(() => RtcpWriter.WriteGenericNack(new byte[12], Me, Console, [1]));
    }

    [Fact]
    public void A_receiver_report_carries_loss_highest_sequence_jitter_and_the_sender_report_echo()
    {
        var statistics = new RtpReceiveStatistics(_clock, 90000);

        // Packets 65534 to 3 sent every 10 ms on a 90 kHz clock and arriving on time; 65535 and 1 are lost.
        (ushort Sequence, int Slot)[] arrivals = [(65534, 0), (0, 2), (2, 4), (3, 5)];
        int slot = 0;

        foreach (var (sequence, at) in arrivals)
        {
            _clock.AdvanceMilliseconds((at - slot) * 10);
            slot = at;
            statistics.Record(Console, sequence, (uint)(at * 900));
        }

        statistics.RecordSenderReport(0x1122334455667788);
        _clock.AdvanceMilliseconds(500);
        var buffer = new byte[64];

        int length = RtcpWriter.WriteReceiverReport(buffer, Me, [statistics]);

        Assert.Equal(
            "81C90007" + "01020304" + "A0B0C0D0" + "55000002" + "00010003" + "00000000" + "33445566" + "00008000",
            Convert.ToHexString(buffer.AsSpan(0, length)));
    }

    [Fact]
    public void A_packet_ten_milliseconds_late_moves_the_jitter_a_sixteenth_of_900_units()
    {
        var statistics = new RtpReceiveStatistics(_clock, 90000);
        statistics.Record(Console, 1, 0);
        _clock.AdvanceMilliseconds(20);

        statistics.Record(Console, 2, 900);

        Assert.Equal(900 / 16.0, statistics.Jitter, 3);
    }

    [Fact]
    public void The_fraction_lost_covers_only_the_interval_since_the_last_report()
    {
        var statistics = new RtpReceiveStatistics(_clock, 90000);
        statistics.Record(Console, 1, 0);
        statistics.Record(Console, 3, 0);
        var block = new byte[24];
        statistics.WriteReportBlock(block);

        statistics.Record(Console, 4, 0);
        statistics.WriteReportBlock(block);

        Assert.Equal(0, block[4]);
        Assert.Equal(1, statistics.CumulativeLost);
    }

    [Fact]
    public void Writing_feedback_and_reports_allocates_nothing()
    {
        var statistics = new RtpReceiveStatistics(_clock, 90000);
        RtpReceiveStatistics[] sources = [statistics];
        ushort[] lost = [1, 2, 40];
        var buffer = new byte[128];
        ushort sequence = 0;

        var report = AllocationMeter.Measure(() =>
        {
            statistics.Record(Console, sequence++, 0);
            RtcpWriter.WriteGenericNack(buffer, Me, Console, lost);
            RtcpWriter.WritePictureLossIndication(buffer, Me, Console);
            RtcpWriter.WriteReceiverReport(buffer, Me, sources);
        });

        Assert.Equal(0, report.BytesPerRun);
    }
}
