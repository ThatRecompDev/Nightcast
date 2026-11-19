// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows.Tests.Reports;

public sealed class PadHatTests
{
    [Theory]
    [InlineData(0, GamepadButtons.DpadUp)]
    [InlineData(1, GamepadButtons.DpadUp | GamepadButtons.DpadRight)]
    [InlineData(2, GamepadButtons.DpadRight)]
    [InlineData(3, GamepadButtons.DpadDown | GamepadButtons.DpadRight)]
    [InlineData(4, GamepadButtons.DpadDown)]
    [InlineData(5, GamepadButtons.DpadDown | GamepadButtons.DpadLeft)]
    [InlineData(6, GamepadButtons.DpadLeft)]
    [InlineData(7, GamepadButtons.DpadUp | GamepadButtons.DpadLeft)]
    [InlineData(8, GamepadButtons.None)]
    [InlineData(15, GamepadButtons.None)]
    public void The_hat_turns_clockwise_from_up(int hat, GamepadButtons expected)
    {
        Assert.Equal(expected, PadHat.Buttons(hat));
    }

    [Fact]
    public void The_face_buttons_in_the_hat_byte_do_not_move_the_d_pad()
    {
        var buttons = PlayStationButtons.Map(0x08 | 0xF0, 0, 0, hasMute: true);

        Assert.Equal(GamepadButtons.South | GamepadButtons.East | GamepadButtons.West | GamepadButtons.North, buttons);
    }
}
