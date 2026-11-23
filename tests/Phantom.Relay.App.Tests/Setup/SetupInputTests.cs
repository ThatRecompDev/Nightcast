// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.App.Setup;
using Phantom.Relay.App.Tests.Input;

namespace Phantom.Relay.App.Tests.Setup;

public sealed class SetupInputTests
{
    private readonly OnePad _pad = new();
    private readonly ReadingApplication _application = new();
    private readonly ApplicationDriver _driver;

    public SetupInputTests()
    {
        _driver = new ApplicationDriver(_application, new InputFrame(InputLimits.Default), _pad);
        _driver.Initialize();
    }

    [Fact]
    public void Keys_pressed_are_read_but_not_their_repeats()
    {
        _driver.Post(new InputEvent(InputEventKind.KeyDown) { Key = Key.Down });
        _driver.Post(new InputEvent(InputEventKind.KeyDown) { Key = Key.Enter });
        _driver.Post(new InputEvent(InputEventKind.KeyDown) { Key = Key.Escape, Repeat = true });

        _driver.Frame(1.0 / 60);

        Assert.Equal(SetupKeys.Down | SetupKeys.Choose, _application.Keys);
    }

    [Fact]
    public void A_pad_button_counts_once_when_pressed_and_not_while_held()
    {
        Frame(GamepadButtons.None);

        Assert.Equal(SetupKeys.Back, Frame(GamepadButtons.East));
        Assert.Equal(SetupKeys.None, Frame(GamepadButtons.East));
        Assert.Equal(SetupKeys.Up | SetupKeys.Choose, Frame(GamepadButtons.East | GamepadButtons.DpadUp | GamepadButtons.South));
    }

    [Fact]
    public void A_button_held_as_the_screen_opens_counts_only_when_pressed_again()
    {
        Assert.Equal(SetupKeys.None, Frame(GamepadButtons.South));
        Assert.Equal(SetupKeys.None, Frame(GamepadButtons.None));
        Assert.Equal(SetupKeys.Choose, Frame(GamepadButtons.South));
    }

    private SetupKeys Frame(GamepadButtons buttons)
    {
        _pad.Pad = new GamepadState(true, buttons, default, default, 0f, 0f, GamepadFeatures.None);
        _driver.Frame(1.0 / 60);

        return _application.Keys;
    }
}
