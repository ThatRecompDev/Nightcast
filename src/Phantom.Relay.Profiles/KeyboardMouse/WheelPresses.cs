// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// Turns wheel notches into presses: each notch holds its direction down for the press time, then up for as long, so
/// a game that reads its pad once a frame sees every notch. Fractional steps (a touchpad, a free-spinning wheel) add
/// up to whole notches; at most <see cref="MaxPendingNotches"/> wait, so a long spin does not press for seconds.
/// </summary>
public sealed class WheelPresses
{
    /// <summary>Notches that wait their turn at most.</summary>
    public const int MaxPendingNotches = 4;

    private const int Directions = (int)WheelDirection.Right + 1;

    private readonly float _pressSeconds;
    private readonly float[] _steps = new float[Directions];
    private readonly int[] _pending = new int[Directions];
    private readonly float[] _remaining = new float[Directions];
    private readonly bool[] _pressed = new bool[Directions];

    public WheelPresses(float pressSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pressSeconds);

        _pressSeconds = pressSeconds;
    }

    /// <summary>Whether <paramref name="direction"/> is pressed this frame.</summary>
    [HotPath]
    public bool IsPressed(WheelDirection direction)
    {
        return _pressed[(int)direction];
    }

    /// <summary>One input frame: the wheel turned <paramref name="wheelX"/>, <paramref name="wheelY"/> steps (y away from the player).</summary>
    [HotPath]
    public void Step(float wheelX, float wheelY, float elapsedSeconds)
    {
        Advance(WheelDirection.Up, MathF.Max(wheelY, 0f), elapsedSeconds);
        Advance(WheelDirection.Down, MathF.Max(-wheelY, 0f), elapsedSeconds);
        Advance(WheelDirection.Right, MathF.Max(wheelX, 0f), elapsedSeconds);
        Advance(WheelDirection.Left, MathF.Max(-wheelX, 0f), elapsedSeconds);
    }

    [HotPath]
    private void Advance(WheelDirection direction, float steps, float elapsedSeconds)
    {
        var d = (int)direction;
        _steps[d] += steps;
        var notches = (int)_steps[d];
        _steps[d] -= notches;
        _pending[d] = Math.Min(_pending[d] + notches, MaxPendingNotches);
        _remaining[d] -= elapsedSeconds;

        // A press or the gap after it is still running.
        if (_remaining[d] > 0f)
        {
            return;
        }

        if (_pressed[d])
        {
            _pressed[d] = false;
            _remaining[d] = _pressSeconds;

            return;
        }

        _remaining[d] = 0f;

        if (_pending[d] > 0)
        {
            _pending[d]--;
            _pressed[d] = true;
            _remaining[d] = _pressSeconds;
        }
    }
}
