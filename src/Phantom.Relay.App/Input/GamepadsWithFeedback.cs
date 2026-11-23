// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.App.Input;

/// <summary>
/// A pad source that only reads, given the feedback side another part offers for the same slots (the engine's XInput
/// source and XInput's motors): reading, the family and the feedback as one.
/// </summary>
public sealed class GamepadsWithFeedback : IGamepadSource, IGamepadFeedback, IGamepadFamilies
{
    private readonly IGamepadSource _source;
    private readonly IGamepadFeedback _feedback;
    private readonly IGamepadFamilies? _families;

    public GamepadsWithFeedback(IGamepadSource source, IGamepadFeedback feedback)
    {
        _source = source;
        _feedback = feedback;
        _families = source as IGamepadFamilies;
    }

    [HotPath]
    public void Poll(Span<GamepadState> pads)
    {
        _source.Poll(pads);
    }

    public GamepadFamily Family(int slot)
    {
        return _families?.Family(slot) ?? GamepadFamily.Unknown;
    }

    public bool TrySetRumble(int gamepad, float lowFrequency, float highFrequency)
    {
        return _feedback.TrySetRumble(gamepad, lowFrequency, highFrequency);
    }

    public bool TrySetLight(int gamepad, float red, float green, float blue)
    {
        return _feedback.TrySetLight(gamepad, red, green, blue);
    }
}
