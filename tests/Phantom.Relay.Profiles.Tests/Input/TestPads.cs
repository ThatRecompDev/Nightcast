// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Testing.Hosting;

namespace Phantom.Relay.Profiles.Tests.Input;

/// <summary>
/// An input frame with pads in it, as a head makes one: the pads are set, one frame runs through the engine's
/// <see cref="ApplicationDriver"/> to poll them, and the frame keeps them (and any key held) for the test to read.
/// </summary>
public sealed class TestPads : IGamepadSource
{
    private readonly GamepadState[] _pads = new GamepadState[InputLimits.DefaultGamepads];
    private readonly ApplicationDriver _driver;

    public TestPads()
    {
        Frame = new InputFrame(InputLimits.Default);
        _driver = new ApplicationDriver(new RecordingApplication(null), Frame, this);
    }

    public InputFrame Frame { get; }

    public static GamepadState Pad(GamepadButtons buttons, Vector2 leftStick, Vector2 rightStick, float rightTrigger)
    {
        return new GamepadState(true, buttons, leftStick, rightStick, 0f, rightTrigger, GamepadFeatures.None);
    }

    /// <summary>Puts <paramref name="pad"/> in <paramref name="slot"/> and polls it into <see cref="Frame"/>.</summary>
    public InputFrame Connect(int slot, GamepadState pad)
    {
        _pads[slot] = pad;
        _driver.Frame(0d);

        return Frame;
    }

    public void Hold(Key key)
    {
        Frame.Inject(new InputEvent(InputEventKind.KeyDown) { Key = key });
    }

    public void Poll(Span<GamepadState> pads)
    {
        _pads.CopyTo(pads);
    }
}
