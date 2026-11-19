// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Profiles.KeyboardMouse;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Tests.Input;

public sealed class PadComposerTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void The_default_profile_plays_the_first_connected_pad()
    {
        var pads = new TestPads();
        var frame = pads.Connect(1, TestPads.Pad(GamepadButtons.South, new Vector2(0f, -1f), Vector2.Zero, 1f));

        var pad = new PadComposer(StreamProfile.Default).Compose(frame, Frame);

        Assert.Equal(XboxButtons.A, pad.Buttons);
        Assert.Equal(1f, pad.LeftY, 5);
        Assert.Equal(1f, pad.RightTrigger, 5);
    }

    [Fact]
    public void The_default_profile_leaves_the_keyboard_out()
    {
        var pads = new TestPads();
        pads.Hold(Key.W);
        pads.Hold(Key.Space);

        var pad = new PadComposer(StreamProfile.Default).Compose(pads.Frame, Frame);

        Assert.Equal(default, pad);
    }

    [Fact]
    public void A_chosen_slot_plays_instead_of_the_first()
    {
        var pads = new TestPads();
        pads.Connect(0, TestPads.Pad(GamepadButtons.South, Vector2.Zero, Vector2.Zero, 0f));
        var frame = pads.Connect(2, TestPads.Pad(GamepadButtons.East, Vector2.Zero, Vector2.Zero, 0f));
        var composer = new PadComposer(StreamProfile.Default) { Slot = 2 };

        var pad = composer.Compose(frame, Frame);

        Assert.Equal(XboxButtons.B, pad.Buttons);
    }

    [Fact]
    public void The_gta_preset_with_a_pad_connected_merges_both()
    {
        var pads = new TestPads();
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.South, new Vector2(0.3f, 0f), new Vector2(1f, 0f), 0.4f));
        pads.Hold(Key.Space);
        pads.Hold(Key.W);
        frame.Inject(new InputEvent(InputEventKind.PointerDown) { Button = PointerButton.Right });

        var pad = new PadComposer(GtaKeyboardMousePreset.Profile).Compose(frame, Frame);

        // Buttons from both hands; the keys push the left stick further, the pad the right one.
        Assert.Equal(XboxButtons.A | XboxButtons.X, pad.Buttons);
        Assert.Equal(0f, pad.LeftX);
        Assert.Equal(1f, pad.LeftY);
        Assert.Equal(1f, pad.RightX, 5);
        Assert.Equal(0f, pad.RightY, 5);

        // The pad's right trigger and the right mouse's left trigger.
        Assert.Equal((0.4f - PadSettings.StandardTriggerDeadZone) / (1f - PadSettings.StandardTriggerDeadZone), pad.RightTrigger, 5);
        Assert.Equal(1f, pad.LeftTrigger);
    }

    [Fact]
    public void The_stick_pushed_further_wins()
    {
        var pads = new TestPads();
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.None, new Vector2(0f, 0.8f), Vector2.Zero, 0f));
        pads.Hold(Key.W);
        pads.Hold(Key.LeftAlt);

        var pad = new PadComposer(GtaKeyboardMousePreset.Profile).Compose(frame, Frame);

        // Walking tilts the keys' stick halfway up; the pad's is further down.
        Assert.True(pad.LeftY < -MovementSettings.StandardWalkTilt);
        Assert.Equal(0f, pad.LeftX, 5);
    }

    [Fact]
    public void Triggers_merge_by_the_further_pull()
    {
        var pad = new XboxPadState(XboxButtons.A, 0f, 0f, 0f, 0f, 0.2f, 0.9f);
        var keyboardMouse = new XboxPadState(XboxButtons.B, 0f, 0f, 0f, 0f, 0.7f, 0.1f);

        var merged = PadMerge.Merge(in pad, in keyboardMouse);

        Assert.Equal(XboxButtons.A | XboxButtons.B, merged.Buttons);
        Assert.Equal(0.7f, merged.LeftTrigger);
        Assert.Equal(0.9f, merged.RightTrigger);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var pads = new TestPads();
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.South | GamepadButtons.DpadUp, new Vector2(0.2f, -0.6f), new Vector2(0.5f, 0.5f), 0.3f));
        pads.Hold(Key.W);
        pads.Hold(Key.Space);
        frame.Inject(new InputEvent(InputEventKind.PointerMoved) { Delta = new Vector2(12f, -4f) });
        var composer = new PadComposer(GtaKeyboardMousePreset.Profile);

        var report = AllocationMeter.Measure(() => composer.Compose(frame, Frame));

        Assert.Equal(0, report.BytesPerRun);
    }
}
