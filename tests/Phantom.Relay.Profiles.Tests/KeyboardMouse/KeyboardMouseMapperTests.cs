// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Profiles.KeyboardMouse;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Tests.KeyboardMouse;

public sealed class KeyboardMouseMapperTests
{
    private const float Frame = 1f / 60f;

    private static readonly KeyboardMouseSettings Settings = new(
        true,
        MovementSettings.Wasd,
        MouseLookSettings.Standard with { SmoothingFrames = 0f },
        [
            new KeyBinding(Key.Space, PadControl.X),
            new KeyBinding(Key.F, PadControl.Y),
            new KeyBinding(Key.Up, PadControl.RightStickUp),
            new KeyBinding(Key.Q, PadControl.LeftTrigger),
        ],
        [
            new MouseButtonBinding(PointerButton.Left, PadControl.RightTrigger),
            new MouseButtonBinding(PointerButton.Middle, PadControl.RightThumb),
        ],
        new WheelSettings(0.05f, [new WheelBinding(WheelDirection.Up, PadControl.DPadUp)]));

    [Fact]
    public void Forward_pushes_the_left_stick_up()
    {
        var pad = new KeyboardMouseMapper(Settings).Map(Input(Key.W), Frame);

        Assert.Equal(0f, pad.LeftX);
        Assert.Equal(1f, pad.LeftY);
    }

    [Fact]
    public void A_diagonal_is_full_tilt_not_more()
    {
        var pad = new KeyboardMouseMapper(Settings).Map(Input(Key.S, Key.A), Frame);

        Assert.Equal(-MathF.Sqrt(0.5f), pad.LeftX, 5);
        Assert.Equal(-MathF.Sqrt(0.5f), pad.LeftY, 5);
    }

    [Fact]
    public void Opposite_keys_cancel()
    {
        var pad = new KeyboardMouseMapper(Settings).Map(Input(Key.A, Key.D, Key.W), Frame);

        Assert.Equal(0f, pad.LeftX);
        Assert.Equal(1f, pad.LeftY);
    }

    [Fact]
    public void The_walk_key_tilts_the_stick_part_of_the_way()
    {
        var pad = new KeyboardMouseMapper(Settings).Map(Input(Key.W, Key.D, Key.LeftAlt), Frame);

        float length = MathF.Sqrt((pad.LeftX * pad.LeftX) + (pad.LeftY * pad.LeftY));
        Assert.Equal(MovementSettings.StandardWalkTilt, length, 5);
    }

    [Fact]
    public void Keys_press_their_buttons_and_pull_their_triggers()
    {
        var pad = new KeyboardMouseMapper(Settings).Map(Input(Key.Space, Key.F, Key.Q, Key.LeftShift), Frame);

        Assert.Equal(XboxButtons.X | XboxButtons.Y | XboxButtons.A, pad.Buttons);
        Assert.Equal(1f, pad.LeftTrigger);
        Assert.Equal(0f, pad.RightTrigger);
    }

    [Fact]
    public void A_key_bound_to_a_stick_direction_pushes_it()
    {
        var pad = new KeyboardMouseMapper(Settings).Map(Input(Key.Up), Frame);

        Assert.Equal(1f, pad.RightY);
        Assert.Equal(0f, pad.LeftY);
    }

    [Fact]
    public void Mouse_buttons_press_their_controls()
    {
        var frame = Input();
        Press(frame, PointerButton.Left);
        Press(frame, PointerButton.Middle);

        var pad = new KeyboardMouseMapper(Settings).Map(frame, Frame);

        Assert.Equal(1f, pad.RightTrigger);
        Assert.Equal(XboxButtons.RightThumb, pad.Buttons);
    }

    [Fact]
    public void The_mouse_drives_the_right_stick_with_y_up()
    {
        var frame = Input();
        frame.Inject(new InputEvent(InputEventKind.PointerMoved) { Delta = new Vector2(0f, -30f) });

        var pad = new KeyboardMouseMapper(Settings).Map(frame, Frame);

        Assert.Equal(0f, pad.RightX);
        Assert.True(pad.RightY > MouseLookSettings.StandardAntiDeadZone);
    }

    [Fact]
    public void A_wheel_notch_presses_for_the_press_time_and_then_lets_go()
    {
        var mapper = new KeyboardMouseMapper(Settings);
        var notch = Input();
        notch.Inject(new InputEvent(InputEventKind.PointerScrolled) { Wheel = new Vector2(0f, 1f) });

        var first = mapper.Map(notch, 0.03f);
        var second = mapper.Map(Input(), 0.03f);
        var third = mapper.Map(Input(), 0.03f);

        Assert.Equal(XboxButtons.DPadUp, first.Buttons);
        Assert.Equal(XboxButtons.DPadUp, second.Buttons);
        Assert.Equal(XboxButtons.None, third.Buttons);
    }

    [Fact]
    public void Two_quick_notches_are_two_presses()
    {
        var presses = new WheelPresses(0.05f);
        var seen = new List<bool>();

        presses.Step(0f, 2f, 0.05f);
        seen.Add(presses.IsPressed(WheelDirection.Up));

        for (var frame = 0; frame < 3; frame++)
        {
            presses.Step(0f, 0f, 0.05f);
            seen.Add(presses.IsPressed(WheelDirection.Up));
        }

        Assert.Equal([true, false, true, false], seen);
    }

    [Fact]
    public void Switched_off_it_is_the_pad_at_rest()
    {
        var mapper = new KeyboardMouseMapper(Settings with { Enabled = false });

        var pad = mapper.Map(Input(Key.W, Key.Space), Frame);

        Assert.Equal(default, pad);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var mapper = new KeyboardMouseMapper(Settings);
        var frame = Input(Key.W, Key.D, Key.Space, Key.Up);
        Press(frame, PointerButton.Left);
        frame.Inject(new InputEvent(InputEventKind.PointerMoved) { Delta = new Vector2(12f, -4f) });

        var report = AllocationMeter.Measure(() => mapper.Map(frame, Frame));

        Assert.Equal(0, report.BytesPerRun);
    }

    private static InputFrame Input(params Key[] held)
    {
        var frame = new InputFrame(InputLimits.Default);

        foreach (var key in held)
        {
            frame.Inject(new InputEvent(InputEventKind.KeyDown) { Key = key });
        }

        return frame;
    }

    private static void Press(InputFrame frame, PointerButton button)
    {
        frame.Inject(new InputEvent(InputEventKind.PointerDown) { Button = button });
    }
}
