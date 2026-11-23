// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests.Mapping;

public sealed class EvdevCapabilitiesTests
{
    [Fact]
    public void A_device_with_the_first_gamepad_button_and_a_left_stick_is_a_pad()
    {
        var keys = new byte[EvdevCapabilities.KeyBytes];
        var axes = new byte[EvdevCapabilities.AxisBytes];
        keys[EvdevCodes.ButtonSouth / 8] = 1 << (EvdevCodes.ButtonSouth % 8);
        axes[0] = 1 << EvdevCodes.AxisX;

        Assert.True(EvdevCapabilities.IsGamepad(keys, axes));
    }

    [Fact]
    public void A_keyboard_a_touchpad_or_a_motion_device_is_not()
    {
        var keys = new byte[EvdevCapabilities.KeyBytes];
        var axes = new byte[EvdevCapabilities.AxisBytes];
        axes[0] = 0b111;

        // Motion: axes without buttons; a keyboard: keys without the gamepad's; then buttons without a stick.
        Assert.False(EvdevCapabilities.IsGamepad(keys, axes));
        keys[0] = 0xFF;
        Assert.False(EvdevCapabilities.IsGamepad(keys, axes));
        keys[EvdevCodes.ButtonSouth / 8] = 1 << (EvdevCodes.ButtonSouth % 8);
        Assert.False(EvdevCapabilities.IsGamepad(keys, new byte[EvdevCapabilities.AxisBytes]));
    }

    [Fact]
    public void A_bit_past_the_bitmap_is_not_set()
    {
        Assert.False(EvdevCapabilities.Has(new byte[] { 0xFF }, 8));
        Assert.True(EvdevCapabilities.Has(new byte[] { 0x00, 0x02 }, 9));
    }

    [Fact]
    public void Steam_input_is_an_xbox_pad_and_sony_a_playstation_pad_with_buttons_by_place()
    {
        Assert.Equal(GamepadFamily.Xbox, Descriptions.SteamVirtualPad().Family);
        Assert.Equal(FaceLayout.XboxLabels, Descriptions.SteamVirtualPad().Layout);
        Assert.Equal(GamepadFamily.PlayStation, Descriptions.DualSense().Family);
        Assert.Equal(FaceLayout.Positional, Descriptions.DualSense().Layout);
        Assert.Equal(GamepadFamily.Unknown, Descriptions.Generic().Family);
    }
}
