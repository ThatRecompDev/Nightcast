// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Testing.Hosting;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Tests.Input;

/// <summary>
/// An input frame with pads in it, as a head makes one: the pads are set, one frame runs through the engine's
/// <see cref="ApplicationDriver"/> to poll them, and the frame keeps them (and any key held) for the test to read.
/// </summary>
public sealed class TestPads : IGamepadSource
{
    private readonly GamepadState[] _pads = new GamepadState[InputLimits.DefaultGamepads];
    private readonly ApplicationDriver _driver;
    private Action<InputFrame>? _during;

    public TestPads()
    {
        Frame = new InputFrame(InputLimits.Default);
        _driver = new ApplicationDriver(new RecordingApplication(frame => _during?.Invoke(frame)), Frame, this);
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

    /// <summary>
    /// Runs one frame and composes inside it, as the app does: the events injected since show in the frame's
    /// <see cref="InputFrame.Events"/> only while it runs.
    /// </summary>
    public XboxPadState Compose(PadComposer composer, float seconds)
    {
        XboxPadState pad = default;
        _during = frame => pad = composer.Compose(frame, seconds);
        _driver.Frame(0d);
        _during = null;

        return pad;
    }

    public void Hold(Key key)
    {
        Frame.Inject(new InputEvent(InputEventKind.KeyDown) { Key = key });
    }

    public void Release(Key key)
    {
        Frame.Inject(new InputEvent(InputEventKind.KeyUp) { Key = key });
    }

    public void Poll(Span<GamepadState> pads)
    {
        _pads.CopyTo(pads);
    }
}
