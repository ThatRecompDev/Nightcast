// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// The player's pad as the console's, under a profile's <see cref="PadSettings"/>: the buttons by place
/// (<see cref="PadButtons"/>) and then remapped (<see cref="ButtonRemap"/>); each stick through the engine's radial
/// dead zone (<see cref="GamepadStick.Shape"/>), then the response curve on its length, its y turned up; each trigger
/// past its dead zone, rescaled to 0..1 again. Made once per profile; a frame allocates nothing.
/// </summary>
public sealed class PhysicalPad
{
    private readonly float _stickDeadZone;
    private readonly float _triggerDeadZone;
    private readonly ResponseCurve _curve;
    private readonly ButtonRemap _remap;

    public PhysicalPad(PadSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.StickDeadZone);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(settings.StickDeadZone, 1f);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.TriggerDeadZone);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(settings.TriggerDeadZone, 1f);

        _stickDeadZone = settings.StickDeadZone;
        _triggerDeadZone = settings.TriggerDeadZone;
        _curve = settings.Curve;
        _remap = new ButtonRemap(settings.Remap);
    }

    /// <summary>
    /// The pad in <paramref name="slot"/>, or in the first connected slot when it is null; the pad at rest when that
    /// slot is empty or out of range.
    /// </summary>
    [HotPath]
    public XboxPadState Read(ReadOnlySpan<GamepadState> pads, int? slot)
    {
        if (slot.HasValue)
        {
            var chosen = slot.Value;

            if (chosen < 0 || chosen >= pads.Length)
            {
                return default;
            }

            return Map(in pads[chosen]);
        }

        foreach (ref readonly var pad in pads)
        {
            if (pad.Connected)
            {
                return Map(in pad);
            }
        }

        return default;
    }

    /// <summary>One pad as the console's; a pad that is not connected is the pad at rest.</summary>
    [HotPath]
    public XboxPadState Map(in GamepadState pad)
    {
        if (!pad.Connected)
        {
            return default;
        }

        var left = Stick(pad.LeftStick);
        var right = Stick(pad.RightStick);
        var buttons = _remap.Apply(PadButtons.ByPlace(pad.Buttons));

        // The engine's sticks are y down, the console's y up.
        return new XboxPadState(
            buttons,
            left.X,
            -left.Y,
            right.X,
            -right.Y,
            Trigger(pad.LeftTrigger),
            Trigger(pad.RightTrigger));
    }

    [HotPath]
    private Vector2 Stick(Vector2 raw)
    {
        var shaped = GamepadStick.Shape(raw, _stickDeadZone);
        var length = shaped.Length();

        if (!(length > 0f))
        {
            return Vector2.Zero;
        }

        return shaped * (ResponseCurves.Apply(_curve, length) / length);
    }

    [HotPath]
    private float Trigger(float raw)
    {
        if (!(raw > _triggerDeadZone))
        {
            return 0f;
        }

        return MathF.Min((raw - _triggerDeadZone) / (1f - _triggerDeadZone), 1f);
    }
}
