// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows.Tests.Reports;

public sealed class OutputReportTests
{
    private static readonly PadOutput Rumble = new(LowMotor: 200, HighMotor: 50, Light: false, Red: 0, Green: 0, Blue: 0, ReleaseLight: false);
    private static readonly PadOutput Orange = new(LowMotor: 0, HighMotor: 0, Light: true, Red: 255, Green: 128, Blue: 0, ReleaseLight: true);

    [Fact]
    public void A_dualsense_usb_report_carries_the_motors_without_touching_the_light()
    {
        var report = new byte[PadOutputReports.MaxLength];

        var length = PadOutputReports.Write(report, PadModel.DualSense, PadTransport.Usb, Rumble, 0);

        Assert.Equal(48, length);
        Assert.Equal(0x02, report[0]);
        Assert.Equal(0x03, report[1]);
        Assert.Equal(0x00, report[2]);
        Assert.Equal(50, report[3]);
        Assert.Equal(200, report[4]);
    }

    [Fact]
    public void A_dualsense_usb_report_sets_the_light_and_releases_it_from_the_pad()
    {
        var report = new byte[PadOutputReports.MaxLength];

        PadOutputReports.Write(report, PadModel.DualSense, PadTransport.Usb, Orange, 0);

        Assert.Equal(0x04, report[2]);
        Assert.Equal(0x02, report[1 + 38]);
        Assert.Equal(0x02, report[1 + 41]);
        Assert.Equal(255, report[1 + 44]);
        Assert.Equal(128, report[1 + 45]);
        Assert.Equal(0, report[1 + 46]);
    }

    [Fact]
    public void A_dualsense_bluetooth_report_is_tagged_and_closed_by_its_crc()
    {
        var report = new byte[PadOutputReports.MaxLength];

        var length = PadOutputReports.Write(report, PadModel.DualSenseEdge, PadTransport.Bluetooth, Rumble, 0x13);

        Assert.Equal(78, length);
        Assert.Equal(0x31, report[0]);
        Assert.Equal(0x30, report[1]);
        Assert.Equal(0x10, report[2]);
        Assert.Equal(0x03, report[3]);
        Assert.Equal(50, report[5]);

        Assert.Equal(200, report[6]);
        Assert.Equal(ReferenceCrc(report.AsSpan(0, 74)), BinaryPrimitives.ReadUInt32LittleEndian(report.AsSpan(74)));
    }

    [Fact]
    public void A_dualshock_usb_report_carries_the_motors_and_the_light()
    {
        var report = new byte[PadOutputReports.MaxLength];

        var length = PadOutputReports.Write(report, PadModel.DualShock4, PadTransport.Usb, Orange with { LowMotor = 9, HighMotor = 7 }, 0);

        Assert.Equal(32, length);
        Assert.Equal(new byte[] { 0x05, 0x03, 0x00, 0x00, 7, 9, 255, 128, 0 }, report[..9]);
    }

    [Fact]
    public void A_dualshock_bluetooth_report_is_closed_by_its_crc()
    {
        var report = new byte[PadOutputReports.MaxLength];

        var length = PadOutputReports.Write(report, PadModel.DualShock4, PadTransport.Bluetooth, Rumble, 0);

        Assert.Equal(78, length);
        Assert.Equal(new byte[] { 0x11, 0xC0, 0x00, 0x01, 0x00, 0x00, 50, 200 }, report[..8]);
        Assert.Equal(ReferenceCrc(report.AsSpan(0, 74)), BinaryPrimitives.ReadUInt32LittleEndian(report.AsSpan(74)));
    }

    [Fact]
    public void The_crc_matches_a_bitwise_crc_of_the_header_and_the_report()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[40] = 0x5A;

        BluetoothOutputCrc.Append(report);

        Assert.Equal(ReferenceCrc(report.AsSpan(0, 74)), BinaryPrimitives.ReadUInt32LittleEndian(report.AsSpan(74)));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 255)]
    [InlineData(0.5f, 128)]
    [InlineData(-3f, 0)]
    [InlineData(7f, 255)]
    [InlineData(float.NaN, 0)]
    public void A_level_becomes_a_byte(float level, byte expected)
    {
        Assert.Equal(expected, PadLevels.ToByte(level));
    }

    /// <summary>CRC-32 (reflected 0xEDB88320) of 0xA2 and then the bytes, one bit at a time.</summary>
    private static uint ReferenceCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        crc = Step(crc, 0xA2);

        foreach (var value in bytes)
        {
            crc = Step(crc, value);
        }

        return ~crc;
    }

    private static uint Step(uint crc, byte value)
    {
        crc ^= value;

        for (var bit = 0; bit < 8; bit++)
        {
            uint mask = (crc & 1) != 0 ? 0xEDB88320u : 0u;
            crc = (crc >> 1) ^ mask;
        }

        return crc;
    }
}
