// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Xbox.Tests.Input;

public sealed class InputReportTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_gamepad_report_has_the_header_then_one_pad_in_its_layout()
    {
        var writer = new InputReportWriter(_clock);
        var bytes = new byte[InputReportWriter.GamepadReportSize];
        var pad = new XboxPadState(XboxButtons.A | XboxButtons.DPadUp, 1f, -1f, 0.5f, 0f, 1f, 0f);
        _clock.AdvanceMilliseconds(250);

        int written = writer.WriteGamepad(bytes, 0, pad);

        Assert.Equal(38, written);
        Assert.Equal((ushort)InputReportKind.Gamepad, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(250d, BinaryPrimitives.ReadDoubleLittleEndian(bytes.AsSpan(6)));

        Assert.Equal(1, bytes[14]);
        Assert.Equal(0, bytes[15]);
        Assert.Equal((ushort)(16 | 256), BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(18)));
        Assert.Equal(-short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(16384, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(22)));

        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28)));
    }

    [Fact]
    public void Each_report_takes_the_next_sequence_number()
    {
        var writer = new InputReportWriter(_clock);
        var bytes = new byte[InputReportWriter.GamepadReportSize];

        writer.WriteClientMetadata(bytes, 0);
        writer.WriteGamepad(bytes, 0, default);

        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(2)));
    }

    [Fact]
    public void The_client_metadata_report_carries_the_touch_point_count()
    {
        var writer = new InputReportWriter(_clock);
        var bytes = new byte[InputReportWriter.ClientMetadataSize];

        int written = writer.WriteClientMetadata(bytes, 5);

        Assert.Equal(15, written);
        Assert.Equal((ushort)InputReportKind.ClientMetadata, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal(5, bytes[14]);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        Assert.Equal(short.MaxValue, InputScales.Axis(3f));
        Assert.Equal(-short.MaxValue, InputScales.Axis(-3f));
        Assert.Equal(0, InputScales.Trigger(-0.5f));
    }

    [Fact]
    public void A_vibration_report_is_read_as_motor_strengths_and_a_duration()
    {
        // u16 kind 128, type 0 (four motors), pad 1, low 50 %, high 100 %, triggers 0 and 25 %, 500 ms, no delay or repeat.
        byte[] data = [128, 0, 0, 1, 50, 100, 0, 25, 0xF4, 0x01, 0, 0, 0];

        bool read = RumbleReportReader.TryRead(data, out var report);

        Assert.True(read);
        Assert.Equal(1, report.PadIndex);
        Assert.Equal(0.5f, report.LowFrequency);
        Assert.Equal(1f, report.HighFrequency);
        Assert.Equal(0.25f, report.RightTrigger);
        Assert.Equal(TimeSpan.FromMilliseconds(500), report.Duration);
    }

    [Fact]
    public void Other_reports_and_short_ones_are_not_vibration()
    {
        Assert.False(RumbleReportReader.TryRead([2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], out _));
        Assert.False(RumbleReportReader.TryRead([128, 0, 0, 1, 50, 100, 0, 25, 0xF4, 0x01, 0, 0], out _));
        Assert.False(RumbleReportReader.TryRead([128, 0, 3, 1, 50, 100, 0, 25, 0xF4, 0x01, 0, 0, 0], out _));
    }

    [Fact]
    public void Server_metadata_is_told_by_its_two_byte_kind()
    {
        Assert.True(ServerMetadataReport.Is([16, 0, 1, 0, 0, 0]));
        Assert.False(ServerMetadataReport.Is([16]));
        Assert.False(ServerMetadataReport.Is([128, 0, 0]));
        Assert.False(ServerMetadataReport.Is([16, 1]));
    }
}
