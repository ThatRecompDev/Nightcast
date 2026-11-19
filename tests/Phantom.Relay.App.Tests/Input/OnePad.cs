// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.App.Tests.Input;

/// <summary>A gamepad source with the test's pad in the first slot and nothing in the others.</summary>
public sealed class OnePad : IGamepadSource
{
    public GamepadState Pad { get; set; }

    public void Poll(Span<GamepadState> pads)
    {
        pads.Clear();
        pads[0] = Pad;
    }
}
