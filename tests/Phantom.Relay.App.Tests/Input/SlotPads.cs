// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.App.Tests.Input;

/// <summary>A gamepad source with pads in the test's slots; it records the feedback it gets and names one family.</summary>
public sealed class SlotPads : IGamepadSource, IGamepadFeedback, IGamepadFamilies
{
    private readonly Dictionary<int, GamepadState> _pads;

    public SlotPads(Dictionary<int, GamepadState> pads)
    {
        _pads = pads;
    }

    public GamepadFamily Kind { get; init; } = GamepadFamily.Unknown;

    public int LastRumbleSlot { get; private set; } = -1;

    public int LastLightSlot { get; private set; } = -1;

    public void Poll(Span<GamepadState> pads)
    {
        pads.Clear();

        foreach (var (slot, pad) in _pads)
        {
            pads[slot] = pad;
        }
    }

    public bool TrySetRumble(int gamepad, float lowFrequency, float highFrequency)
    {
        LastRumbleSlot = gamepad;

        return true;
    }

    public bool TrySetLight(int gamepad, float red, float green, float blue)
    {
        LastLightSlot = gamepad;

        return true;
    }

    public GamepadFamily Family(int slot)
    {
        return Kind;
    }
}
