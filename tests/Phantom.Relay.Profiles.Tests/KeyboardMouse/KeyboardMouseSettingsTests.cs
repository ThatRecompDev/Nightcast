// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Profiles.KeyboardMouse;

namespace Phantom.Relay.Profiles.Tests.KeyboardMouse;

public sealed class KeyboardMouseSettingsTests
{
    private static readonly StreamProfile Custom = StreamProfile.Default with
    {
        Name = "Mine",
        TitleIds = ["1A2B3C4D"],
        KeyboardMouse = new KeyboardMouseSettings(
            true,
            MovementSettings.Wasd with { Walk = Key.CapsLock, WalkTilt = 0.4f },
            MouseLookSettings.Standard with { SensitivityY = 0.8f, AimButton = PointerButton.Button4 },
            [new KeyBinding(Key.Space, PadControl.X), new KeyBinding(Key.Up, PadControl.RightStickUp)],
            [new MouseButtonBinding(PointerButton.Left, PadControl.RightTrigger)],
            new WheelSettings(0.1f, [new WheelBinding(WheelDirection.Down, PadControl.DPadDown)])),
    };

    [Fact]
    public void Keyboard_and_mouse_settings_survive_the_store()
    {
        var store = new ProfileStore(new MemoryPersistentStore());

        store.Save(Custom);
        var loaded = Assert.Single(store.LoadAll()).KeyboardMouse;

        Assert.True(loaded.Enabled);
        Assert.Equal(Custom.KeyboardMouse.Movement, loaded.Movement);
        Assert.Equal(Custom.KeyboardMouse.MouseLook, loaded.MouseLook);
        Assert.Equal(Custom.KeyboardMouse.Keys, loaded.Keys);
        Assert.Equal(Custom.KeyboardMouse.MouseButtons, loaded.MouseButtons);

        Assert.Equal(0.1f, loaded.Wheel.PressSeconds);
        Assert.Equal(Custom.KeyboardMouse.Wheel.Bindings, loaded.Wheel.Bindings);
    }

    [Fact]
    public void Keys_buttons_and_controls_are_written_by_name()
    {
        var memory = new MemoryPersistentStore();

        new ProfileStore(memory).Save(Custom);
        string json = Encoding.UTF8.GetString(memory.Read("profile.Mine")!);

        Assert.Contains("\"key\": \"Space\"", json, StringComparison.Ordinal);
        Assert.Contains("\"control\": \"RightStickUp\"", json, StringComparison.Ordinal);
        Assert.Contains("\"button\": \"Left\"", json, StringComparison.Ordinal);
        Assert.Contains("\"aimButton\": \"Button4\"", json, StringComparison.Ordinal);
        Assert.Contains("\"direction\": \"Down\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_saved_before_keyboard_and_mouse_loads_with_them_off()
    {
        var store = new ProfileStore(new MemoryPersistentStore());

        store.Save(Custom with { KeyboardMouse = null! });
        var loaded = Assert.Single(store.LoadAll());

        Assert.Same(KeyboardMouseSettings.Off, loaded.KeyboardMouse);
    }

    [Fact]
    public void The_default_profile_leaves_keyboard_and_mouse_off()
    {
        Assert.False(StreamProfile.Default.KeyboardMouse.Enabled);
    }
}
