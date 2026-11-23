// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests.Mapping;

public sealed class EvdevPadStateTests
{
    [Fact]
    public void A_pad_at_rest_is_connected_with_everything_centred()
    {
        var pad = new EvdevPadState(Descriptions.DualSense());

        var state = pad.State();

        Assert.True(state.Connected);
        Assert.Equal(GamepadButtons.None, state.Buttons);
        Assert.Equal(0f, state.LeftStick.X, 2);
        Assert.Equal(0f, state.RightStick.Y, 2);
        Assert.Equal(0f, state.LeftTrigger);
        Assert.Equal(GamepadFeatures.Rumble, state.Features);
    }

    [Theory]
    [InlineData(EvdevCodes.ButtonSouth, GamepadButtons.South)]
    [InlineData(EvdevCodes.ButtonEast, GamepadButtons.East)]
    [InlineData(EvdevCodes.ButtonX, GamepadButtons.West)]
    [InlineData(EvdevCodes.ButtonY, GamepadButtons.North)]
    [InlineData(EvdevCodes.ButtonLeftShoulder, GamepadButtons.LeftShoulder)]
    [InlineData(EvdevCodes.ButtonRightShoulder, GamepadButtons.RightShoulder)]
    [InlineData(EvdevCodes.ButtonSelect, GamepadButtons.Back)]
    [InlineData(EvdevCodes.ButtonStart, GamepadButtons.Start)]
    [InlineData(EvdevCodes.ButtonMode, GamepadButtons.Guide)]
    [InlineData(EvdevCodes.ButtonLeftThumb, GamepadButtons.LeftStick)]
    [InlineData(EvdevCodes.ButtonRightThumb, GamepadButtons.RightStick)]
    [InlineData(EvdevCodes.ButtonDpadUp, GamepadButtons.DpadUp)]
    [InlineData(EvdevCodes.ButtonDpadRight, GamepadButtons.DpadRight)]
    public void Steam_input_buttons_are_read_by_place_with_xbox_labels(ushort code, GamepadButtons expected)
    {
        var pad = new EvdevPadState(Descriptions.SteamVirtualPad());

        pad.Apply(new InputEvent(EvdevCodes.Key, code, 1));

        Assert.Equal(expected, pad.State().Buttons);
    }

    [Fact]
    public void A_playstation_pad_sends_its_top_and_left_buttons_by_place()
    {
        var pad = new EvdevPadState(Descriptions.DualSense());

        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonX, 1));
        var north = pad.State().Buttons;
        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonX, 0));
        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonY, 1));

        Assert.Equal(GamepadButtons.North, north);
        Assert.Equal(GamepadButtons.West, pad.State().Buttons);
    }

    [Fact]
    public void A_button_let_go_is_no_longer_held()
    {
        var pad = new EvdevPadState(Descriptions.SteamVirtualPad());

        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonSouth, 1));
        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonEast, 1));
        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonSouth, 0));

        Assert.Equal(GamepadButtons.East, pad.State().Buttons);
    }

    [Fact]
    public void Sticks_run_from_minus_one_to_one_over_the_range_with_y_down()
    {
        var pad = new EvdevPadState(Descriptions.SteamVirtualPad());

        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisX, 32767));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisY, -32768));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisRx, -16384));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisRy, 32767));
        var state = pad.State();

        Assert.Equal(1f, state.LeftStick.X, 3);
        Assert.Equal(-1f, state.LeftStick.Y, 3);
        Assert.Equal(-0.5f, state.RightStick.X, 3);
        Assert.Equal(1f, state.RightStick.Y, 3);
    }

    [Fact]
    public void Analog_triggers_run_from_zero_to_one_and_press_past_the_threshold()
    {
        var pad = new EvdevPadState(Descriptions.SteamVirtualPad());

        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisZ, 255));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisRz, 20));
        var state = pad.State();

        Assert.Equal(1f, state.LeftTrigger, 3);
        Assert.Equal(20f / 255f, state.RightTrigger, 3);
        Assert.Equal(GamepadButtons.LeftTrigger, state.Buttons);
    }

    [Fact]
    public void A_pad_without_trigger_axes_reads_its_digital_triggers_as_full_travel()
    {
        var pad = new EvdevPadState(Descriptions.Generic());

        pad.Apply(new InputEvent(EvdevCodes.Key, EvdevCodes.ButtonRightTrigger, 1));
        var state = pad.State();

        Assert.Equal(0f, state.LeftTrigger);
        Assert.Equal(1f, state.RightTrigger);
        Assert.Equal(GamepadButtons.RightTrigger, state.Buttons);
        Assert.Equal(GamepadFeatures.None, state.Features);
    }

    [Theory]
    [InlineData(-1, 0, GamepadButtons.DpadLeft)]
    [InlineData(1, 0, GamepadButtons.DpadRight)]
    [InlineData(0, -1, GamepadButtons.DpadUp)]
    [InlineData(0, 1, GamepadButtons.DpadDown)]
    [InlineData(1, -1, GamepadButtons.DpadUp | GamepadButtons.DpadRight)]
    [InlineData(0, 0, GamepadButtons.None)]
    public void The_hat_is_the_dpad(int x, int y, GamepadButtons expected)
    {
        var pad = new EvdevPadState(Descriptions.SteamVirtualPad());

        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisHatX, x));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisHatY, y));

        Assert.Equal(expected, pad.State().Buttons);
    }

    [Fact]
    public void Events_of_other_types_and_codes_change_nothing()
    {
        var pad = new EvdevPadState(Descriptions.SteamVirtualPad());

        pad.Apply(new InputEvent(EvdevCodes.Key, 0x1E, 1));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, 0x3F, 99));
        pad.Apply(new InputEvent(EvdevCodes.Absolute, 0x200, 99));
        pad.Apply(new InputEvent(0x04, 0x04, 1));

        Assert.Equal(GamepadButtons.None, pad.State().Buttons);
    }
}
