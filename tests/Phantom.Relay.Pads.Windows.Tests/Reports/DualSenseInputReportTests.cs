// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows.Tests.Reports;

public sealed class DualSenseInputReportTests
{
    /// <summary>A USB report 0x01: the shared block starts at byte 1.</summary>
    internal static byte[] UsbReport()
    {
        var report = new byte[64];
        report[0] = 0x01;
        FillBlock(report.AsSpan(1));

        return report;
    }

    /// <summary>A Bluetooth report 0x31: a sequence tag, then the shared block at byte 2.</summary>
    internal static byte[] BluetoothReport()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[1] = 0x70;
        FillBlock(report.AsSpan(2));

        return report;
    }

    [Fact]
    public void A_usb_report_reads_sticks_triggers_buttons_and_motion()
    {
        var report = UsbReport();

        var read = DualSenseInputReport.TryRead(report, PadTransport.Usb, out var state, out var motion);

        Assert.True(read);
        AssertBlock(state, motion);
    }

    [Fact]
    public void A_bluetooth_report_reads_the_same_block_after_its_sequence_tag()
    {
        var report = BluetoothReport();

        var read = DualSenseInputReport.TryRead(report, PadTransport.Bluetooth, out var state, out var motion);

        Assert.True(read);
        AssertBlock(state, motion);
    }

    [Fact]
    public void The_short_report_a_bluetooth_pad_sends_before_it_is_switched_is_not_read()
    {
        var report = UsbReport();

        var read = DualSenseInputReport.TryRead(report, PadTransport.Bluetooth, out var state, out _);

        Assert.False(read);
        Assert.False(state.Connected);
    }

    [Fact]
    public void A_report_too_short_is_not_read()
    {
        var report = UsbReport()[..20];

        Assert.False(DualSenseInputReport.TryRead(report, PadTransport.Usb, out _, out _));
    }

    [Theory]
    [InlineData((byte)0, -1f)]
    [InlineData((byte)128, 0f)]
    [InlineData((byte)255, 1f)]
    public void A_stick_pushed_up_reads_negative_y_as_the_engine_wants(byte raw, float expected)
    {
        var report = UsbReport();
        report[2] = raw;

        DualSenseInputReport.TryRead(report, PadTransport.Usb, out var state, out _);

        Assert.Equal(expected, state.LeftStick.Y, 3);
    }

    [Fact]
    public void A_report_at_rest_holds_nothing()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[8] = 0x08;

        // Both sticks centred.
        report[1] = 128;
        report[2] = 128;
        report[3] = 128;
        report[4] = 128;

        DualSenseInputReport.TryRead(report, PadTransport.Usb, out var state, out _);

        Assert.Equal(GamepadButtons.None, state.Buttons);
        Assert.Equal(0f, state.LeftStick.Length(), 3);
        Assert.Equal(0f, state.RightStick.Length(), 3);
    }

    private static void FillBlock(Span<byte> block)
    {
        block[0] = 255;
        block[1] = 0;
        block[2] = 0;
        block[3] = 128;
        block[4] = 255;
        block[5] = 0;

        // Hat 2 (right) and cross; L1, R2 (digital), create and L3; PS, touchpad click and mute.
        block[7] = 0x02 | 0x20;
        block[8] = 0x01 | 0x08 | 0x10 | 0x40;
        block[9] = 0x01 | 0x02 | 0x04;

        // Gyro 0x0102, -2, 3.
        block[15] = 0x02;
        block[16] = 0x01;
        block[17] = 0xFE;
        block[18] = 0xFF;
        block[19] = 0x03;

        // Accelerometer 8192, -8192, 0.
        block[22] = 0x20;
        block[24] = 0xE0;

        // Sensor time 0x01020304.
        block[27] = 0x04;
        block[28] = 0x03;
        block[29] = 0x02;
        block[30] = 0x01;
    }

    private static void AssertBlock(GamepadState state, PadMotion motion)
    {
        var expected = GamepadButtons.DpadRight | GamepadButtons.South | GamepadButtons.LeftShoulder
            | GamepadButtons.RightTrigger | GamepadButtons.Back | GamepadButtons.LeftStick
            | GamepadButtons.Guide | GamepadButtons.Touchpad | GamepadButtons.Misc;

        Assert.True(state.Connected);
        Assert.Equal(expected, state.Buttons);
        Assert.Equal(GamepadFeatures.Rumble | GamepadFeatures.Light | GamepadFeatures.Touchpad, state.Features);

        Assert.Equal(1f, state.LeftStick.X, 3);
        Assert.Equal(-1f, state.LeftStick.Y, 3);
        Assert.Equal(-1f, state.RightStick.X, 3);
        Assert.Equal(0f, state.RightStick.Y, 3);
        Assert.Equal(1f, state.LeftTrigger);
        Assert.Equal(0f, state.RightTrigger);

        Assert.Equal(new PadMotion(0x0102, -2, 3, 8192, -8192, 0, 0x01020304, 0), motion);
    }
}
