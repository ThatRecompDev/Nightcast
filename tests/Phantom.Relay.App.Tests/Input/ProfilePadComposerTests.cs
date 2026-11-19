// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.App.Input;
using Phantom.Relay.Profiles;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class ProfilePadComposerTests
{
    private readonly OnePad _pad = new();
    private readonly ComposingApplication _application = new(new ProfilePadComposer(StreamProfile.Default));

    [Fact]
    public void The_pad_plays_through_the_profile_with_buttons_by_place()
    {
        _pad.Pad = new GamepadState(true, GamepadButtons.South | GamepadButtons.Start, Vector2.Zero, Vector2.Zero, 0f, 1f, default);

        RunFrame();

        Assert.Equal(XboxButtons.A | XboxButtons.Menu, _application.Composed.Buttons);
        Assert.Equal(1f, _application.Composed.RightTrigger);
    }

    [Fact]
    public void A_stick_pushed_up_reaches_the_console_as_up()
    {
        _pad.Pad = new GamepadState(true, GamepadButtons.None, new Vector2(0f, -1f), Vector2.Zero, 0f, 0f, default);

        RunFrame();

        Assert.True(_application.Composed.LeftY > 0.9f);
    }

    [Fact]
    public void Without_a_pad_the_console_pad_rests()
    {
        _pad.Pad = default;

        RunFrame();

        Assert.Equal(default, _application.Composed);
    }

    private void RunFrame()
    {
        var driver = new ApplicationDriver(_application, new InputFrame(InputLimits.Default), _pad);
        driver.Initialize();
        driver.Frame(1.0 / 60);
    }
}
