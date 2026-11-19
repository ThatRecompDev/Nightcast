// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Pads.Windows.Reports;
using Phantom.Relay.Pads.Windows.Tests.Devices;
using Phantom.Relay.Pads.Windows.Tests.Reports;

namespace Phantom.Relay.Pads.Windows.Tests;

public sealed class PlayStationPadsTests
{
    private const ushort Sony = 0x054C;
    private const int Usb = 64;
    private const int Bluetooth = 78;

    private readonly FakeHidHost _host = new();
    private readonly ManualPadWorkers _workers = new();
    private readonly GamepadState[] _pads = new GamepadState[4];

    [Fact]
    public void A_dualsense_on_usb_is_read_into_the_first_slot()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("usb#1", Sony, 0x0CE6, Usb);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.UsbReport());

        _workers.StepAll();
        pads.Poll(_pads);

        Assert.True(_pads[0].Connected);
        Assert.True(_pads[0].IsDown(GamepadButtons.South | GamepadButtons.Misc));
        Assert.False(_pads[1].Connected);
        Assert.Equal(GamepadFamily.PlayStation, pads.Family(0));
        Assert.Equal(GamepadFamily.Unknown, pads.Family(1));
    }

    [Fact]
    public void A_slot_stays_empty_until_its_pad_sends_a_full_report()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("bt#1", Sony, 0x0CE6, Bluetooth);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.UsbReport());

        _workers.StepAll();
        pads.Poll(_pads);

        Assert.False(_pads[0].Connected);
        Assert.Equal(GamepadFamily.Unknown, pads.Family(0));
    }

    [Theory]
    [InlineData((ushort)0x0CE6, Bluetooth, new byte[] { 0x05 })]
    [InlineData((ushort)0x0DF2, Bluetooth, new byte[] { 0x05 })]
    [InlineData((ushort)0x09CC, Bluetooth, new byte[] { 0x02 })]
    [InlineData((ushort)0x05C4, Usb, new byte[0])]
    [InlineData((ushort)0x0CE6, Usb, new byte[0])]
    public void A_bluetooth_pad_is_switched_to_its_full_report_by_a_feature_read(ushort product, int inputLength, byte[] expected)
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("pad", Sony, product, inputLength);

        pads.Rescan();

        Assert.Equal(expected, device.FeatureReads);
    }

    [Fact]
    public void A_bluetooth_dualsense_is_read_from_report_0x31()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("bt#1", Sony, 0x0CE6, Bluetooth);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.BluetoothReport());

        _workers.StepAll();
        pads.Poll(_pads);

        Assert.True(_pads[0].IsDown(GamepadButtons.DpadRight | GamepadButtons.Guide));
        Assert.Equal(-1f, _pads[0].LeftStick.Y, 3);
    }

    [Fact]
    public void Other_devices_are_asked_once_while_present_and_never_opened()
    {
        using var pads = new PlayStationPads(_host, _workers);
        _host.Plug("keyboard", 0x046D, 0xC31C, Usb);
        _host.Plug("xbox", 0x045E, 0x0B12, Usb);

        pads.Rescan();
        pads.Rescan();

        Assert.Equal(1, _host.Identifications["keyboard"]);
        Assert.Equal(1, _host.Identifications["xbox"]);
        Assert.Empty(_workers.Readers);
    }

    [Fact]
    public void A_pad_another_program_holds_is_tried_again_at_the_next_rescan()
    {
        using var pads = new PlayStationPads(_host, _workers);
        _host.Plug("pad", Sony, 0x0CE6, Usb);
        _host.Busy.Add("pad");
        pads.Rescan();
        _host.Busy.Clear();

        pads.Rescan();

        Assert.Single(_workers.Readers);
    }

    [Fact]
    public void A_pad_unplugged_empties_its_slot_and_comes_back_when_plugged_in_again()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var first = _host.Plug("usb#1", Sony, 0x0CE6, Usb);
        pads.Rescan();
        first.Queue(DualSenseInputReportTests.UsbReport());
        _workers.StepAll();

        // Unplugged: the reader finds it gone, closes it and empties the slot.
        _host.Unplug("usb#1");
        _workers.StepAll();
        pads.Poll(_pads);

        Assert.False(_pads[0].Connected);
        Assert.True(first.Disposed);

        // Plugged in again (a new path, as Windows gives it): the next rescan reads it into the same slot.
        var again = _host.Plug("usb#2", Sony, 0x0CE6, Usb);
        pads.Rescan();
        again.Queue(DualSenseInputReportTests.UsbReport());
        _workers.StepAll();
        pads.Poll(_pads);

        Assert.True(_pads[0].Connected);
        Assert.Equal(2, _workers.Readers.Count);
    }

    [Fact]
    public void Pads_fill_the_slots_in_order_and_a_new_pad_takes_the_first_free_one()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var first = _host.Plug("a", Sony, 0x0CE6, Usb);
        pads.Rescan();
        var second = _host.Plug("b", Sony, 0x05C4, Usb);
        pads.Rescan();

        // The first pad leaves; a third one arrives.
        _host.Unplug("a");
        _workers.StepAll();
        var third = _host.Plug("c", Sony, 0x0DF2, Usb);
        pads.Rescan();
        third.Queue(DualSenseInputReportTests.UsbReport());
        _workers.StepAll();

        Assert.True(first.Disposed);
        Assert.False(second.Disposed);
        Assert.Equal(new[] { PadModel.DualSense, PadModel.DualShock4, PadModel.DualSenseEdge }, _workers.Readers.Select(static reader => reader.Model));
        Assert.True(pads.TryGetMotion(0, out _));
    }

    [Fact]
    public void A_fifth_pad_waits_for_a_free_slot()
    {
        using var pads = new PlayStationPads(_host, _workers);

        for (var pad = 0; pad < 5; pad++)
        {
            _host.Plug($"pad{pad}", Sony, 0x0CE6, Usb);
        }

        pads.Rescan();

        Assert.Equal(4, _workers.Readers.Count);
    }

    [Fact]
    public void Feedback_to_an_empty_slot_is_refused()
    {
        using var pads = new PlayStationPads(_host, _workers);

        Assert.False(pads.TrySetRumble(0, 1f, 1f));
        Assert.False(pads.TrySetLight(0, 1f, 0f, 0f));
        Assert.False(pads.TrySetRumble(9, 1f, 1f));
        Assert.False(pads.TrySetRumble(-1, 1f, 1f));
    }

    [Fact]
    public void Rumble_wakes_the_reader_which_writes_it_once()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("usb#1", Sony, 0x0CE6, Usb);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.UsbReport());
        _workers.StepAll();

        var taken = pads.TrySetRumble(0, 1f, 0.5f);
        _workers.StepAll();
        _workers.StepAll();

        Assert.True(taken);
        Assert.Equal(1, device.Wakes);
        var written = Assert.Single(device.Writes);
        Assert.Equal(48, written.Length);
        Assert.Equal(128, written[3]);
        Assert.Equal(255, written[4]);
    }

    [Fact]
    public void The_light_takes_a_bluetooth_dualsense_from_its_own_animation_once()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("bt#1", Sony, 0x0CE6, Bluetooth);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.BluetoothReport());
        _workers.StepAll();

        pads.TrySetLight(0, 0f, 0f, 1f);
        _workers.StepAll();
        pads.TrySetLight(0, 1f, 0f, 0f);
        _workers.StepAll();

        Assert.Equal(2, device.Writes.Count);
        Assert.Equal(0x02, device.Writes[0][3 + 41]);
        Assert.Equal(255, device.Writes[0][3 + 46]);
        Assert.Equal(0x00, device.Writes[1][3 + 41]);
        Assert.Equal(255, device.Writes[1][3 + 44]);
        Assert.Equal(0x10, device.Writes[1][1]);
    }

    [Fact]
    public void Motion_is_read_into_its_side_channel_with_a_growing_sequence()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("usb#1", Sony, 0x0CE6, Usb);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.UsbReport());
        device.Queue(DualSenseInputReportTests.UsbReport());

        _workers.StepAll();
        _workers.StepAll();
        var read = pads.TryGetMotion(0, out var motion);

        Assert.True(read);
        Assert.Equal(0x0102, motion.GyroX);
        Assert.Equal(2u, motion.Sequence);
        Assert.False(pads.TryGetMotion(1, out _));
    }

    [Fact]
    public void Stop_closes_every_pad_and_empties_every_slot()
    {
        var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("usb#1", Sony, 0x0CE6, Usb);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.UsbReport());
        _workers.StepAll();

        pads.Dispose();
        pads.Poll(_pads);

        Assert.True(device.Disposed);
        Assert.False(_pads[0].Connected);
    }

    [Fact]
    public void Polling_allocates_nothing()
    {
        using var pads = new PlayStationPads(_host, _workers);
        var device = _host.Plug("usb#1", Sony, 0x0CE6, Usb);
        pads.Rescan();
        device.Queue(DualSenseInputReportTests.UsbReport());
        _workers.StepAll();
        var buffer = _pads;

        var report = AllocationMeter.Measure(() =>
        {
            pads.Poll(buffer);
            pads.Family(0);
            pads.TryGetMotion(0, out _);
        });

        Assert.Equal(0, report.BytesPerRun);
        Assert.True(buffer[0].Connected);
    }
}
