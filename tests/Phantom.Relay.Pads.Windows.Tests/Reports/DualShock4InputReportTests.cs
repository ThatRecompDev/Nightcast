// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows.Tests.Reports;

public sealed class DualShock4InputReportTests
{
    [Fact]
    public void A_usb_report_reads_sticks_triggers_buttons_and_motion()
    {
        var report = new byte[64];
        report[0] = 0x01;
        FillBlock(report.AsSpan(1));

        var read = DualShock4InputReport.TryRead(report, PadTransport.Usb, out var state, out var motion);

        Assert.True(read);
        AssertBlock(state, motion);
    }

    [Fact]
    public void A_bluetooth_report_reads_the_same_block_after_its_two_extra_bytes()
    {
        var report = new byte[78];
        report[0] = 0x11;
        report[1] = 0xC0;
        FillBlock(report.AsSpan(3));

        var read = DualShock4InputReport.TryRead(report, PadTransport.Bluetooth, out var state, out var motion);

        Assert.True(read);
        AssertBlock(state, motion);
    }

    [Fact]
    public void The_report_counter_in_the_system_byte_is_not_a_mute_button()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[5] = 0x08;
        report[7] = 0xFC;

        DualShock4InputReport.TryRead(report, PadTransport.Usb, out var state, out _);

        Assert.Equal(GamepadButtons.None, state.Buttons & GamepadButtons.Misc);
    }

    [Fact]
    public void A_dualsense_report_id_over_bluetooth_is_not_read_as_a_dualshock()
    {
        var report = new byte[78];
        report[0] = 0x31;

        Assert.False(DualShock4InputReport.TryRead(report, PadTransport.Bluetooth, out _, out _));
    }

    private static void FillBlock(Span<byte> block)
    {
        block[0] = 128;
        block[1] = 255;
        block[2] = 255;
        block[3] = 0;

        // Hat 7 (up-left) with triangle and circle; R1, options and R3; PS.
        block[4] = 0x07 | 0x80 | 0x40;
        block[5] = 0x02 | 0x20 | 0x80;
        block[6] = 0x01;

        // L2 halfway, R2 fully; sensor time 0x0A0B.
        block[7] = 128;
        block[8] = 255;
        block[9] = 0x0B;
        block[10] = 0x0A;

        // Gyro -1, 0, 2; accelerometer 1, 2, 3.
        block[12] = 0xFF;
        block[13] = 0xFF;
        block[16] = 0x02;
        block[18] = 0x01;
        block[20] = 0x02;
        block[22] = 0x03;
    }

    private static void AssertBlock(GamepadState state, PadMotion motion)
    {
        var expected = GamepadButtons.DpadUp | GamepadButtons.DpadLeft | GamepadButtons.North | GamepadButtons.East
            | GamepadButtons.RightShoulder | GamepadButtons.Start | GamepadButtons.RightStick | GamepadButtons.Guide;

        Assert.True(state.Connected);
        Assert.Equal(expected, state.Buttons);
        Assert.Equal(0f, state.LeftStick.X, 3);
        Assert.Equal(1f, state.LeftStick.Y, 3);
        Assert.Equal(1f, state.RightStick.X, 3);
        Assert.Equal(-1f, state.RightStick.Y, 3);

        Assert.Equal(128f / 255f, state.LeftTrigger, 4);
        Assert.Equal(1f, state.RightTrigger);
        Assert.Equal(new PadMotion(-1, 0, 2, 1, 2, 3, 0x0A0B, 0), motion);
    }
}
