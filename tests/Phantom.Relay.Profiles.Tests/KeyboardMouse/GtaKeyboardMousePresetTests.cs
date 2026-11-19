// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Content.Storage;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Profiles.KeyboardMouse;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Tests.KeyboardMouse;

public sealed class GtaKeyboardMousePresetTests
{
    private const float Frame = 1f / 60f;

    [Theory]
    [InlineData(Key.LeftShift, XboxButtons.A)]
    [InlineData(Key.Space, XboxButtons.X)]
    [InlineData(Key.F, XboxButtons.Y)]
    [InlineData(Key.R, XboxButtons.B)]
    [InlineData(Key.Q, XboxButtons.RightShoulder)]
    [InlineData(Key.Tab, XboxButtons.LeftShoulder)]
    [InlineData(Key.E, XboxButtons.DPadRight)]
    [InlineData(Key.M, XboxButtons.View)]
    [InlineData(Key.Escape, XboxButtons.Menu)]
    [InlineData(Key.C, XboxButtons.RightThumb)]
    [InlineData(Key.V, XboxButtons.None)]
    public void Each_key_presses_its_button(Key key, XboxButtons expected)
    {
        var frame = new InputFrame(InputLimits.Default);
        frame.Inject(new InputEvent(InputEventKind.KeyDown) { Key = key });

        var pad = new KeyboardMouseMapper(GtaKeyboardMousePreset.Settings).Map(frame, Frame);

        Assert.Equal(expected, pad.Buttons);
    }

    [Fact]
    public void Left_mouse_shoots_and_right_mouse_aims()
    {
        var frame = new InputFrame(InputLimits.Default);
        frame.Inject(new InputEvent(InputEventKind.PointerDown) { Button = PointerButton.Left });
        frame.Inject(new InputEvent(InputEventKind.PointerDown) { Button = PointerButton.Right });

        var pad = new KeyboardMouseMapper(GtaKeyboardMousePreset.Settings).Map(frame, Frame);

        Assert.Equal(1f, pad.RightTrigger);
        Assert.Equal(1f, pad.LeftTrigger);
        Assert.Equal(XboxButtons.None, pad.Buttons);
    }

    [Fact]
    public void The_preset_survives_the_store_as_it_was_built()
    {
        var store = new ProfileStore(new MemoryPersistentStore());
        var preset = GtaKeyboardMousePreset.Profile;

        store.Save(preset);
        var loaded = Assert.Single(store.LoadAll());

        Assert.Equal("GTA VI (keyboard & mouse)", loaded.Name);
        Assert.True(loaded.KeyboardMouse.Enabled);
        Assert.Equal(preset.KeyboardMouse.Movement, loaded.KeyboardMouse.Movement);
        Assert.Equal(preset.KeyboardMouse.MouseLook, loaded.KeyboardMouse.MouseLook);
        Assert.Equal(preset.KeyboardMouse.Keys, loaded.KeyboardMouse.Keys);
        Assert.Equal(preset.KeyboardMouse.MouseButtons, loaded.KeyboardMouse.MouseButtons);

        Assert.Empty(loaded.KeyboardMouse.Wheel.Bindings);
    }
}
