// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Two gamepad sources as one (XInput's Xbox pads and the PlayStation pads, for instance): the first keeps its own
/// slots, the second's connected pads fill the free ones in order. Feedback and the pad's family go to whichever
/// source owns a slot this frame. Nothing is allocated per frame.
/// </summary>
public sealed class CombinedGamepads : IGamepadSource, IGamepadFeedback, IGamepadFamilies
{
    private const int NoSlot = -1;

    private readonly IGamepadSource _first;
    private readonly IGamepadSource _second;
    private readonly IGamepadFeedback? _firstFeedback;
    private readonly IGamepadFeedback? _secondFeedback;
    private readonly IGamepadFamilies? _firstFamilies;
    private readonly IGamepadFamilies? _secondFamilies;
    private readonly GamepadState[] _secondPads;
    private readonly int[] _secondSlotOf;

    public CombinedGamepads(IGamepadSource first, IGamepadSource second, int slots)
    {
        _first = first;
        _second = second;
        _secondPads = new GamepadState[slots];
        _secondSlotOf = new int[slots];
        Array.Fill(_secondSlotOf, NoSlot);

        // What each source can do beside reading its pads.
        _firstFeedback = first as IGamepadFeedback;
        _secondFeedback = second as IGamepadFeedback;
        _firstFamilies = first as IGamepadFamilies;
        _secondFamilies = second as IGamepadFamilies;
    }

    [HotPath]
    public void Poll(Span<GamepadState> pads)
    {
        _first.Poll(pads);
        var second = _secondPads.AsSpan(0, Math.Min(pads.Length, _secondPads.Length));
        _second.Poll(second);
        Array.Fill(_secondSlotOf, NoSlot);
        int next = 0;

        for (int index = 0; index < second.Length; index++)
        {
            if (!second[index].Connected)
            {
                continue;
            }

            next = FreeSlot(pads, next);

            if (next == NoSlot)
            {
                return;
            }

            pads[next] = second[index];
            _secondSlotOf[next] = index;
            next++;
        }
    }

    public bool TrySetRumble(int gamepad, float lowFrequency, float highFrequency)
    {
        int second = SecondSlot(gamepad);

        if (second != NoSlot)
        {
            return _secondFeedback is not null && _secondFeedback.TrySetRumble(second, lowFrequency, highFrequency);
        }

        return _firstFeedback is not null && _firstFeedback.TrySetRumble(gamepad, lowFrequency, highFrequency);
    }

    public bool TrySetLight(int gamepad, float red, float green, float blue)
    {
        int second = SecondSlot(gamepad);

        if (second != NoSlot)
        {
            return _secondFeedback is not null && _secondFeedback.TrySetLight(second, red, green, blue);
        }

        return _firstFeedback is not null && _firstFeedback.TrySetLight(gamepad, red, green, blue);
    }

    public GamepadFamily Family(int slot)
    {
        int second = SecondSlot(slot);

        if (second != NoSlot && _secondFamilies is not null)
        {
            return _secondFamilies.Family(second);
        }

        if (second == NoSlot && _firstFamilies is not null)
        {
            return _firstFamilies.Family(slot);
        }

        return GamepadFamily.Unknown;
    }

    private static int FreeSlot(Span<GamepadState> pads, int from)
    {
        for (int slot = from; slot < pads.Length; slot++)
        {
            if (!pads[slot].Connected)
            {
                return slot;
            }
        }

        return NoSlot;
    }

    private int SecondSlot(int slot)
    {
        if (slot < 0 || slot >= _secondSlotOf.Length)
        {
            return NoSlot;
        }

        return _secondSlotOf[slot];
    }
}
