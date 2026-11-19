// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.App.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class DirectPadComposerTests
{
    private readonly OnePad _source = new();
    private readonly ComposingApplication _application = new(new DirectPadComposer());
    private readonly ApplicationDriver _driver;

    public DirectPadComposerTests()
    {
        _driver = new ApplicationDriver(_application, new InputFrame(InputLimits.Default), _source);
    }

    [Fact]
    public void Buttons_map_by_their_place_on_the_pad()
    {
        _source.Pad = Connected(GamepadButtons.South | GamepadButtons.North | GamepadButtons.Back | GamepadButtons.Start | GamepadButtons.Guide | GamepadButtons.DpadLeft | GamepadButtons.RightStick);

        _driver.Frame(0.016);

        var expected = XboxButtons.A | XboxButtons.Y | XboxButtons.View | XboxButtons.Menu | XboxButtons.Nexus | XboxButtons.DPadLeft | XboxButtons.RightThumb;
        Assert.Equal(expected, _application.Composed.Buttons);
    }

    [Fact]
    public void Sticks_turn_y_up_and_triggers_keep_their_travel()
    {
        _source.Pad = new GamepadState(true, GamepadButtons.None, new Vector2(0.5f, -1f), new Vector2(-0.25f, 0.75f), 0.2f, 1f, default);

        _driver.Frame(0.016);

        Assert.Equal(new XboxPadState(XboxButtons.None, 0.5f, 1f, -0.25f, -0.75f, 0.2f, 1f), _application.Composed);
    }

    [Fact]
    public void A_pad_that_is_not_connected_rests()
    {
        _source.Pad = new GamepadState(false, GamepadButtons.South, Vector2.One, Vector2.One, 1f, 1f, default);

        _driver.Frame(0.016);

        Assert.Equal(default, _application.Composed);
    }

    private static GamepadState Connected(GamepadButtons buttons)
    {
        return new GamepadState(true, buttons, Vector2.Zero, Vector2.Zero, 0f, 0f, default);
    }
}
