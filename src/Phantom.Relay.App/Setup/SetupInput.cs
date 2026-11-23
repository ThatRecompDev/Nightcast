// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.App.Setup;

/// <summary>
/// A frame's input as the setup screen's keys: a key pressed (not its repeat) or a pad button newly held on any pad.
/// Buttons held when the screen opened count only once let go and pressed again.
/// </summary>
public sealed class SetupInput
{
    private GamepadButtons _held;
    private bool _started;

    public SetupKeys Read(InputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var keys = Keys(input);
        var held = GamepadButtons.None;

        foreach (var pad in input.Gamepads)
        {
            if (pad.Connected)
            {
                held |= pad.Buttons;
            }
        }

        var pressed = held & ~_held;
        _held = held;

        if (!_started)
        {
            _started = true;

            return keys;
        }

        return keys | Buttons(pressed);
    }

    private static SetupKeys Keys(InputFrame input)
    {
        var keys = SetupKeys.None;

        foreach (var inputEvent in input.Events)
        {
            if (inputEvent.Kind == InputEventKind.KeyDown && !inputEvent.Repeat)
            {
                keys |= FromKey(inputEvent.Key);
            }
        }

        return keys;
    }

    private static SetupKeys FromKey(Key key)
    {
        switch (key)
        {
            case Key.Up:
                return SetupKeys.Up;

            case Key.Down:
                return SetupKeys.Down;

            case Key.Enter:
                return SetupKeys.Choose;

            case Key.Escape:
                return SetupKeys.Back;

            default:
                return SetupKeys.None;
        }
    }

    private static SetupKeys Buttons(GamepadButtons pressed)
    {
        var keys = SetupKeys.None;
        keys |= Bit(pressed, GamepadButtons.DpadUp, SetupKeys.Up) | Bit(pressed, GamepadButtons.DpadDown, SetupKeys.Down);
        keys |= Bit(pressed, GamepadButtons.South, SetupKeys.Choose) | Bit(pressed, GamepadButtons.East, SetupKeys.Back);

        return keys;
    }

    private static SetupKeys Bit(GamepadButtons pressed, GamepadButtons button, SetupKeys key)
    {
        if ((pressed & button) != 0)
        {
            return key;
        }

        return SetupKeys.None;
    }
}
