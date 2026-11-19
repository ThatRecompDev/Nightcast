// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Tests.Input;

public sealed class PhysicalPadTests
{
    private static readonly PadSettings NoDeadZones = new(0f, 0f, ResponseCurve.Linear, new Dictionary<XboxButtons, XboxButtons>());

    [Theory]
    [InlineData(GamepadButtons.South, XboxButtons.A)]
    [InlineData(GamepadButtons.East, XboxButtons.B)]
    [InlineData(GamepadButtons.West, XboxButtons.X)]
    [InlineData(GamepadButtons.North, XboxButtons.Y)]
    [InlineData(GamepadButtons.LeftShoulder, XboxButtons.LeftShoulder)]
    [InlineData(GamepadButtons.RightShoulder, XboxButtons.RightShoulder)]
    [InlineData(GamepadButtons.Back, XboxButtons.View)]
    [InlineData(GamepadButtons.Start, XboxButtons.Menu)]
    [InlineData(GamepadButtons.LeftStick, XboxButtons.LeftThumb)]
    [InlineData(GamepadButtons.RightStick, XboxButtons.RightThumb)]
    [InlineData(GamepadButtons.DpadUp, XboxButtons.DPadUp)]
    [InlineData(GamepadButtons.DpadDown, XboxButtons.DPadDown)]
    [InlineData(GamepadButtons.DpadLeft, XboxButtons.DPadLeft)]
    [InlineData(GamepadButtons.DpadRight, XboxButtons.DPadRight)]
    [InlineData(GamepadButtons.Guide, XboxButtons.Nexus)]
    [InlineData(GamepadButtons.Touchpad, XboxButtons.View)]
    [InlineData(GamepadButtons.LeftTrigger, XboxButtons.None)]
    [InlineData(GamepadButtons.Misc, XboxButtons.None)]
    public void Buttons_map_by_their_place(GamepadButtons place, XboxButtons expected)
    {
        var pad = new PhysicalPad(PadSettings.Standard).Map(Pad(place));

        Assert.Equal(expected, pad.Buttons);
    }

    [Fact]
    public void Several_buttons_map_together()
    {
        var pad = new PhysicalPad(PadSettings.Standard).Map(Pad(GamepadButtons.South | GamepadButtons.North | GamepadButtons.Start));

        Assert.Equal(XboxButtons.A | XboxButtons.Y | XboxButtons.Menu, pad.Buttons);
    }

    [Fact]
    public void The_remap_sends_a_button_as_another_and_passes_the_rest()
    {
        var remap = new Dictionary<XboxButtons, XboxButtons> { [XboxButtons.A] = XboxButtons.B, [XboxButtons.X] = XboxButtons.None };
        var physical = new PhysicalPad(PadSettings.Standard with { Remap = remap });

        var pad = physical.Map(Pad(GamepadButtons.South | GamepadButtons.West | GamepadButtons.North));

        Assert.Equal(XboxButtons.B | XboxButtons.Y, pad.Buttons);
    }

    [Fact]
    public void A_stick_pushed_down_on_the_pad_is_down_on_the_console()
    {
        var pad = new PhysicalPad(NoDeadZones).Map(Pad(GamepadButtons.None) with { LeftStick = new Vector2(0.5f, 0.8f) });

        Assert.Equal(0.5f, pad.LeftX, 5);
        Assert.Equal(-0.8f, pad.LeftY, 5);
    }

    [Fact]
    public void A_stick_inside_the_dead_zone_rests_and_beyond_it_is_rescaled_by_the_engines_shape()
    {
        var physical = new PhysicalPad(NoDeadZones with { StickDeadZone = 0.2f });

        var resting = physical.Map(Pad(GamepadButtons.None) with { RightStick = new Vector2(0.15f, 0f) });
        var pushed = physical.Map(Pad(GamepadButtons.None) with { RightStick = new Vector2(0.6f, 0f) });

        Assert.Equal(0f, resting.RightX);
        Assert.Equal(GamepadStick.Shape(new Vector2(0.6f, 0f), 0.2f).X, pushed.RightX, 5);
        Assert.Equal(0.5f, pushed.RightX, 5);
    }

    [Theory]
    [InlineData(ResponseCurve.Linear, 0.5f)]
    [InlineData(ResponseCurve.Precise, 0.25f)]
    [InlineData(ResponseCurve.Aggressive, 0.70710677f)]
    [InlineData(ResponseCurve.Smooth, 0.5f)]
    public void The_curve_shapes_the_travel(ResponseCurve curve, float expected)
    {
        var physical = new PhysicalPad(NoDeadZones with { Curve = curve });

        var pad = physical.Map(Pad(GamepadButtons.None) with { LeftStick = new Vector2(-0.5f, 0f) });

        Assert.Equal(-expected, pad.LeftX, 5);
        Assert.Equal(0f, pad.LeftY, 5);
    }

    [Fact]
    public void Every_curve_keeps_the_ends()
    {
        foreach (var curve in Enum.GetValues<ResponseCurve>())
        {
            Assert.Equal(0f, ResponseCurves.Apply(curve, 0f));
            Assert.Equal(1f, ResponseCurves.Apply(curve, 1f), 5);
        }
    }

    [Fact]
    public void A_trigger_inside_its_dead_zone_rests_and_beyond_it_is_rescaled()
    {
        var physical = new PhysicalPad(NoDeadZones with { TriggerDeadZone = 0.1f });

        var pad = physical.Map(Pad(GamepadButtons.None) with { LeftTrigger = 0.05f, RightTrigger = 0.55f });

        Assert.Equal(0f, pad.LeftTrigger);
        Assert.Equal(0.5f, pad.RightTrigger, 5);
    }

    [Fact]
    public void The_first_connected_slot_plays_unless_one_is_chosen()
    {
        var physical = new PhysicalPad(PadSettings.Standard);
        GamepadState[] pads = [default, Pad(GamepadButtons.South), Pad(GamepadButtons.East), default];

        Assert.Equal(XboxButtons.A, physical.Read(pads, null).Buttons);
        Assert.Equal(XboxButtons.B, physical.Read(pads, 2).Buttons);
        Assert.Equal(default, physical.Read(pads, 3));
        Assert.Equal(default, physical.Read(pads, 7));
    }

    [Fact]
    public void A_disconnected_pad_is_the_pad_at_rest()
    {
        var pad = new PhysicalPad(PadSettings.Standard).Map(Pad(GamepadButtons.South) with { Connected = false });

        Assert.Equal(default, pad);
    }

    [Fact]
    public void Reading_a_pad_allocates_nothing()
    {
        var remap = new Dictionary<XboxButtons, XboxButtons> { [XboxButtons.A] = XboxButtons.B };
        var physical = new PhysicalPad(PadSettings.Standard with { Curve = ResponseCurve.Smooth, Remap = remap });
        GamepadState[] pads = [Pad(GamepadButtons.South | GamepadButtons.DpadUp) with { LeftStick = new Vector2(0.4f, -0.7f) }];

        var report = AllocationMeter.Measure(() => physical.Read(pads, null));

        Assert.Equal(0, report.BytesPerRun);
    }

    private static GamepadState Pad(GamepadButtons buttons)
    {
        return new GamepadState(true, buttons, Vector2.Zero, Vector2.Zero, 0f, 0f, GamepadFeatures.None);
    }
}
