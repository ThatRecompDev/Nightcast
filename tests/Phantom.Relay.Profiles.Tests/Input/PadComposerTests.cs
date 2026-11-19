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
    public void With_a_pad_connected_the_mouse_and_keys_play_alone_once_used()
    {
        // A connected pad at rest with a drifting right stick, under its threshold.
        var pads = new TestPads();
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.None, Vector2.Zero, new Vector2(0.2f, 0.1f), 0f));
        pads.Hold(Key.W);
        frame.Inject(new InputEvent(InputEventKind.PointerDown) { Button = PointerButton.Right });
        frame.Inject(new InputEvent(InputEventKind.PointerMoved) { Delta = new Vector2(3f, 0f) });
        var composer = new PadComposer(GtaKeyboardMousePreset.Profile);

        var pad = composer.Compose(frame, Frame);

        Assert.Equal(PadSource.KeyboardMouse, composer.Source);
        Assert.Equal(1f, pad.LeftY);
        Assert.Equal(1f, pad.LeftTrigger);
        Assert.True(pad.RightX > 0f);
        Assert.Equal(0f, pad.RightY, 5);
    }

    [Fact]
    public void The_pad_takes_over_when_it_is_used_and_the_keyboard_is_not()
    {
        var pads = new TestPads();
        pads.Hold(Key.W);
        var composer = new PadComposer(GtaKeyboardMousePreset.Profile);
        pads.Compose(composer, Frame);
        Assert.Equal(PadSource.KeyboardMouse, composer.Source);

        pads.Release(Key.W);
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.South, new Vector2(0f, 0.8f), Vector2.Zero, 0f));

        var pad = composer.Compose(frame, Frame);

        Assert.Equal(PadSource.Controller, composer.Source);
        Assert.Equal(XboxButtons.A, pad.Buttons);
        Assert.True(pad.LeftY < 0f);
    }

    [Fact]
    public void The_controller_wins_a_frame_where_both_are_used()
    {
        var pads = new TestPads();
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.South, Vector2.Zero, Vector2.Zero, 0f));
        pads.Hold(Key.W);
        frame.Inject(new InputEvent(InputEventKind.PointerMoved) { Delta = new Vector2(8f, 0f) });
        var composer = new PadComposer(GtaKeyboardMousePreset.Profile);

        var pad = composer.Compose(frame, Frame);

        Assert.Equal(PadSource.Controller, composer.Source);
        Assert.Equal(XboxButtons.A, pad.Buttons);
        Assert.Equal(0f, pad.LeftY);
    }

    [Fact]
    public void A_mouse_jitter_does_not_take_the_pad()
    {
        var pads = new TestPads();
        var frame = pads.Frame;
        frame.Inject(new InputEvent(InputEventKind.PointerMoved) { Delta = new Vector2(1f, 0f) });
        var composer = new PadComposer(GtaKeyboardMousePreset.Profile);

        composer.Compose(frame, Frame);

        Assert.Equal(PadSource.Controller, composer.Source);
    }

    [Fact]
    public void Without_keyboard_and_mouse_in_the_profile_the_keys_never_take_the_pad()
    {
        var pads = new TestPads();
        pads.Hold(Key.W);
        var composer = new PadComposer(StreamProfile.Default);

        pads.Compose(composer, Frame);

        Assert.Equal(PadSource.Controller, composer.Source);
    }

    [Fact]
    public void Idle_hands_keep_the_source_and_the_pad_rests()
    {
        var pads = new TestPads();
        pads.Hold(Key.W);
        var composer = new PadComposer(GtaKeyboardMousePreset.Profile);
        pads.Compose(composer, Frame);
        Assert.Equal(PadSource.KeyboardMouse, composer.Source);

        pads.Release(Key.W);
        var frame = pads.Connect(0, TestPads.Pad(GamepadButtons.None, new Vector2(0.1f, 0f), Vector2.Zero, 0.05f));

        var pad = composer.Compose(frame, Frame);

        Assert.Equal(PadSource.KeyboardMouse, composer.Source);
        Assert.Equal(default, pad);
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
