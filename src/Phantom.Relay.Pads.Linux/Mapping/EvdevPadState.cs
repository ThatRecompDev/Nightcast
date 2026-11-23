// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Linux.Events;

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// One pad's state as its events build it, and as the engine reads it (<see cref="State"/>): the buttons by place
/// (<see cref="EvdevButtons"/>), the sticks from -1 to 1 over each axis' own range with y down (the kernel's gamepad
/// convention, which the engine shares), the triggers from 0 to 1 on <c>ABS_Z</c>/<c>ABS_RZ</c>, else
/// <c>ABS_BRAKE</c>/<c>ABS_GAS</c>, else their digital press, and the d-pad from the hat or its own buttons. Used by
/// one reader thread; nothing is allocated after construction.
/// </summary>
internal sealed class EvdevPadState
{
    /// <summary>NOT DATA: a trigger past this travel counts as pressed (about XInput's threshold, 30 of 255).</summary>
    public const float PressedTrigger = 0.12f;

    private readonly EvdevAxisRange[] _ranges;
    private readonly int[] _axes = new int[EvdevCodes.AxisCount];
    private readonly FaceLayout _layout;
    private readonly GamepadFeatures _features;
    private GamepadButtons _keys;

    public EvdevPadState(EvdevDescription description)
    {
        _ranges = description.Axes;
        _layout = description.Layout;
        _features = description.Features;

        for (var code = 0; code < _axes.Length && code < _ranges.Length; code++)
        {
            _axes[code] = _ranges[code].Value;
        }
    }

    /// <summary>Takes one key or axis event; any other event is ignored.</summary>
    [HotPath]
    public void Apply(InputEvent input)
    {
        if (input.Type == EvdevCodes.Key)
        {
            var button = EvdevButtons.Map(input.Code, _layout);
            _keys &= ~button;

            if (input.Value != 0)
            {
                _keys |= button;
            }

            return;
        }

        if (input.Type == EvdevCodes.Absolute && input.Code < _axes.Length)
        {
            _axes[input.Code] = input.Value;
        }
    }

    /// <summary>The pad as the engine reads it now.</summary>
    [HotPath]
    public GamepadState State()
    {
        var left = Vector2.Create(Stick(EvdevCodes.AxisX), Stick(EvdevCodes.AxisY));
        var right = Vector2.Create(Stick(EvdevCodes.AxisRx), Stick(EvdevCodes.AxisRy));
        var leftTrigger = Trigger(EvdevCodes.AxisZ, EvdevCodes.AxisBrake, GamepadButtons.LeftTrigger);
        var rightTrigger = Trigger(EvdevCodes.AxisRz, EvdevCodes.AxisGas, GamepadButtons.RightTrigger);
        var buttons = _keys | Hat() | Pressed(leftTrigger, GamepadButtons.LeftTrigger) | Pressed(rightTrigger, GamepadButtons.RightTrigger);

        return new GamepadState(true, buttons, left, right, leftTrigger, rightTrigger, _features);
    }

    [HotPath]
    private float Stick(ushort code)
    {
        return Range(code).Stick(_axes[code]);
    }

    /// <summary>The trigger's travel from its axis, its other axis, or its digital press.</summary>
    [HotPath]
    private float Trigger(ushort axis, ushort other, GamepadButtons press)
    {
        if (Range(axis).Present)
        {
            return Range(axis).Trigger(_axes[axis]);
        }

        if (Range(other).Present)
        {
            return Range(other).Trigger(_axes[other]);
        }

        if ((_keys & press) != 0)
        {
            return 1f;
        }

        return 0f;
    }

    [HotPath]
    private static GamepadButtons Pressed(float travel, GamepadButtons button)
    {
        if (travel > PressedTrigger)
        {
            return button;
        }

        return GamepadButtons.None;
    }

    /// <summary>The d-pad from the hat: x below its centre is left, above right; y below up, above down.</summary>
    [HotPath]
    private GamepadButtons Hat()
    {
        var x = Stick(EvdevCodes.AxisHatX);
        var y = Stick(EvdevCodes.AxisHatY);
        var held = GamepadButtons.None;
        held |= Pressed(-x, GamepadButtons.DpadLeft) | Pressed(x, GamepadButtons.DpadRight);
        held |= Pressed(-y, GamepadButtons.DpadUp) | Pressed(y, GamepadButtons.DpadDown);

        return held;
    }

    [HotPath]
    private EvdevAxisRange Range(ushort code)
    {
        if (code >= _ranges.Length)
        {
            return default;
        }

        return _ranges[code];
    }
}
