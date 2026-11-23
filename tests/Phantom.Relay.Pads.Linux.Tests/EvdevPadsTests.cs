// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;
using Phantom.Relay.Pads.Linux.Tests.Devices;
using Phantom.Relay.Pads.Linux.Tests.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests;

public sealed class EvdevPadsTests
{
    private static readonly InputEvent Report = new(EvdevCodes.Synchronize, EvdevCodes.Report, 0);

    private readonly FakeEvdevHost _host = new();
    private readonly ManualPadWorkers _workers = new();
    private readonly GamepadState[] _pads = new GamepadState[4];

    [Fact]
    public void Steam_inputs_virtual_pad_is_read_into_the_first_slot_as_an_xbox_pad()
    {
        using var pads = Pads();
        var device = _host.Plug("/dev/input/event7", Descriptions.SteamVirtualPad());
        pads.Rescan();
        device.Queue(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonSouth, 1), new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisX, 32767), Report);

        _workers.StepAll();
        pads.Poll(_pads);

        Assert.True(_pads[0].IsDown(GamepadButtons.South));
        Assert.Equal(1f, _pads[0].LeftStick.X, 3);
        Assert.False(_pads[1].Connected);
        Assert.Equal(GamepadFamily.Xbox, pads.Family(0));
        Assert.Equal(GamepadFamily.Unknown, pads.Family(1));
    }

    [Fact]
    public void A_pad_is_in_its_slot_from_the_state_read_when_it_opened()
    {
        using var pads = Pads();
        var device = _host.Plug("/dev/input/event3", Descriptions.SteamVirtualPad());
        device.Current.Add(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonStart, 1));

        pads.Rescan();
        pads.Poll(_pads);

        Assert.Equal(1, device.Resynchronisations);
        Assert.True(_pads[0].IsDown(GamepadButtons.Start));
    }

    [Fact]
    public void Events_are_published_only_at_the_report_that_ends_them()
    {
        using var pads = Pads();
        var device = _host.Plug("/dev/input/event3", Descriptions.SteamVirtualPad());
        pads.Rescan();
        device.Queue(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonEast, 1));
        device.Queue(Report);

        _workers.StepAll();
        pads.Poll(_pads);
        var before = _pads[0].Buttons;
        _workers.StepAll();
        pads.Poll(_pads);

        Assert.Equal(GamepadButtons.None, before);
        Assert.Equal(GamepadButtons.East, _pads[0].Buttons);
    }

    [Fact]
    public void After_a_drop_the_events_until_the_report_are_discarded_and_the_state_read_again()
    {
        using var pads = Pads();
        var device = _host.Plug("/dev/input/event3", Descriptions.SteamVirtualPad());
        pads.Rescan();
        device.Current.Add(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonY, 1));
        var dropped = new InputEvent(EvdevCodes.Synchronize, EvdevCodes.Dropped, 0);

        device.Queue(dropped, new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonSouth, 1), Report);
        _workers.StepAll();
        pads.Poll(_pads);

        Assert.Equal(2, device.Resynchronisations);
        Assert.Equal(GamepadButtons.North, _pads[0].Buttons);
    }

    [Fact]
    public void Devices_that_are_no_pads_are_asked_once_while_present_and_never_opened()
    {
        using var pads = Pads();
        _host.Plug("/dev/input/event0", null);
        _host.Plug("/dev/input/event1", null);

        pads.Rescan();
        pads.Rescan();

        Assert.Equal(1, _host.Descriptions["/dev/input/event0"]);
        Assert.Equal(1, _host.Descriptions["/dev/input/event1"]);
        Assert.Empty(_workers.Readers);
    }

    [Fact]
    public void A_pad_steam_already_reads_is_left_alone()
    {
        using var pads = new EvdevPads(_host, _workers, IgnoredPads.Parse("0x054C/0x0CE6"));
        _host.Plug("/dev/input/event4", Descriptions.DualSense());
        _host.Plug("/dev/input/event9", Descriptions.SteamVirtualPad());

        pads.Rescan();
        pads.Poll(_pads);

        Assert.Single(_workers.Readers);
        Assert.Equal(GamepadFamily.Xbox, pads.Family(0));
        Assert.False(_pads[1].Connected);
    }

    [Fact]
    public void A_pad_that_cannot_be_opened_yet_is_tried_again_at_the_next_rescan()
    {
        using var pads = Pads();
        _host.Plug("/dev/input/event5", Descriptions.SteamVirtualPad());
        _host.Busy.Add("/dev/input/event5");
        pads.Rescan();
        _host.Busy.Clear();

        pads.Rescan();

        Assert.Single(_workers.Readers);
    }

    [Fact]
    public void A_pad_unplugged_empties_its_slot_and_a_new_pad_takes_it()
    {
        using var pads = Pads();
        var first = _host.Plug("/dev/input/event5", Descriptions.SteamVirtualPad());
        pads.Rescan();

        _host.Unplug("/dev/input/event5");
        _workers.StepAll();
        pads.Poll(_pads);

        Assert.False(_pads[0].Connected);
        Assert.True(first.Disposed);

        _host.Plug("/dev/input/event6", Descriptions.DualSense());
        pads.Rescan();
        pads.Poll(_pads);

        Assert.True(_pads[0].Connected);
        Assert.Equal(GamepadFamily.PlayStation, pads.Family(0));
    }

    [Fact]
    public void A_fifth_pad_waits_for_a_free_slot()
    {
        using var pads = Pads();

        for (var pad = 0; pad < 5; pad++)
        {
            _host.Plug($"/dev/input/event{pad}", Descriptions.SteamVirtualPad());
        }

        pads.Rescan();

        Assert.Equal(4, _workers.Readers.Count);
    }

    [Fact]
    public void Rumble_wakes_the_reader_which_writes_it_once_and_a_repeat_is_not_written_again()
    {
        using var pads = Pads();
        var device = _host.Plug("/dev/input/event2", Descriptions.SteamVirtualPad());
        pads.Rescan();

        var taken = pads.TrySetRumble(0, 1f, 0.5f);
        pads.TrySetRumble(0, 1f, 0.5f);
        _workers.StepAll();
        _workers.StepAll();
        pads.TrySetRumble(0, 0f, 0f);
        _workers.StepAll();

        Assert.True(taken);
        Assert.Equal(2, device.Wakes);
        Assert.Equal(new[] { ((ushort)65535, (ushort)32768), ((ushort)0, (ushort)0) }, device.Rumbles);
    }

    [Fact]
    public void Feedback_to_an_empty_slot_or_a_pad_without_rumble_is_refused_and_there_is_no_light()
    {
        using var pads = Pads();
        _host.Plug("/dev/input/event2", Descriptions.Generic());
        pads.Rescan();

        Assert.False(pads.TrySetRumble(0, 1f, 1f));
        Assert.False(pads.TrySetRumble(1, 1f, 1f));
        Assert.False(pads.TrySetRumble(9, 1f, 1f));
        Assert.False(pads.TrySetRumble(-1, 1f, 1f));
        Assert.False(pads.TrySetLight(0, 1f, 0f, 0f));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 65535)]
    [InlineData(2f, 65535)]
    [InlineData(-1f, 0)]
    [InlineData(float.NaN, 0)]
    [InlineData(0.5f, 32768)]
    public void Levels_become_motor_magnitudes(float level, int expected)
    {
        Assert.Equal(expected, EvdevPads.Magnitude(level));
    }

    [Fact]
    public void Stop_closes_every_pad_and_empties_every_slot()
    {
        var pads = Pads();
        var device = _host.Plug("/dev/input/event2", Descriptions.SteamVirtualPad());
        pads.Rescan();

        pads.Dispose();
        pads.Poll(_pads);

        Assert.True(device.Disposed);
        Assert.False(_pads[0].Connected);
    }

    [Fact]
    public void Polling_allocates_nothing()
    {
        using var pads = Pads();
        _host.Plug("/dev/input/event2", Descriptions.SteamVirtualPad());
        pads.Rescan();
        var buffer = _pads;

        var report = AllocationMeter.Measure(() =>
        {
            pads.Poll(buffer);
            pads.Family(0);
        });

        Assert.Equal(0, report.BytesPerRun);
        Assert.True(buffer[0].Connected);
    }

    private EvdevPads Pads()
    {
        return new EvdevPads(_host, _workers, IgnoredPads.None);
    }
}
