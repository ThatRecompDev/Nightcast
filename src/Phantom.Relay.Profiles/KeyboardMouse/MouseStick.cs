// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// The right stick a mouse drives, one input frame at a time (<see cref="MouseLookSettings"/>): the frame's movement
/// over its elapsed time is a speed, the speed a tilt through <see cref="MouseStickCurve"/>, the tilt smoothed; when
/// the mouse stops, the stick falls back to the centre at full tilt per <see cref="MouseLookSettings.DecaySeconds"/>,
/// and once inside the anti-dead-zone (where the game does not turn anyway) it is centred. Time is an argument; the
/// stick reads no clock. <see cref="X"/> and <see cref="Y"/> are the console's: -1..1, +Y up.
/// </summary>
public sealed class MouseStick
{
    /// <summary>A tilt the input report rounds to zero anyway: the decay's last float steps never leave a sliver.</summary>
    private const float RoundsToZero = 0.5f / short.MaxValue;

    private readonly float _scaleX;
    private readonly float _scaleY;
    private readonly float _exponent;
    private readonly float _antiDeadZone;
    private readonly float _follow;
    private readonly float _decayPerSecond;
    private readonly float _aimMultiplier;

    public MouseStick(MouseLookSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.FullTiltSpeed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.Exponent);

        ArgumentOutOfRangeException.ThrowIfNegative(settings.AntiDeadZone);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(settings.AntiDeadZone, 1f);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.SmoothingFrames);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.SmoothingFrames, MouseLookSettings.MaxSmoothingFrames);

        // Mouse y grows downwards and the console's stick y upwards: a move away from the player looks up.
        float flipY = settings.InvertY ? 1f : -1f;
        _scaleX = settings.SensitivityX / settings.FullTiltSpeed;
        _scaleY = flipY * settings.SensitivityY / settings.FullTiltSpeed;
        _exponent = settings.Exponent;
        _antiDeadZone = settings.AntiDeadZone;

        // Each frame moves the stick this share of the way to its target: all of it without smoothing.
        _follow = 1f / (1f + settings.SmoothingFrames);
        _decayPerSecond = DecayRate(settings.DecaySeconds);
        _aimMultiplier = settings.AimMultiplier;
    }

    /// <summary>The stick's x, -1 (left) to 1 (right).</summary>
    public float X { get; private set; }

    /// <summary>The stick's y, -1 (down) to 1 (up).</summary>
    public float Y { get; private set; }

    /// <summary>
    /// One input frame: the mouse moved <paramref name="deltaX"/>, <paramref name="deltaY"/> counts (y down) over
    /// <paramref name="elapsedSeconds"/>, with the aim button held or not. A frame with no time leaves the stick.
    /// </summary>
    [HotPath]
    public void Step(float deltaX, float deltaY, float elapsedSeconds, bool aiming)
    {
        if (!(elapsedSeconds > 0f))
        {
            return;
        }

        float multiplier = aiming ? _aimMultiplier : 1f;
        float speedX = deltaX / elapsedSeconds * _scaleX * multiplier;
        float speedY = deltaY / elapsedSeconds * _scaleY * multiplier;
        float speed = MathF.Sqrt((speedX * speedX) + (speedY * speedY));

        if (speed > 0f)
        {
            float tilt = MouseStickCurve.Tilt(speed, _exponent, _antiDeadZone);
            Follow(speedX / speed * tilt, speedY / speed * tilt);

            return;
        }

        Decay(elapsedSeconds);
    }

    /// <summary>Centres the stick at once: the mouse was released, or the stream paused.</summary>
    public void Reset()
    {
        X = 0f;
        Y = 0f;
    }

    private static float DecayRate(float decaySeconds)
    {
        if (decaySeconds > 0f)
        {
            return 1f / decaySeconds;
        }

        return float.PositiveInfinity;
    }

    [HotPath]
    private void Follow(float targetX, float targetY)
    {
        float x = X + ((targetX - X) * _follow);
        float y = Y + ((targetY - Y) * _follow);
        float length = MathF.Sqrt((x * x) + (y * y));

        if (length > 1f)
        {
            x /= length;
            y /= length;
        }

        X = x;
        Y = y;
    }

    [HotPath]
    private void Decay(float elapsedSeconds)
    {
        float length = MathF.Sqrt((X * X) + (Y * Y));
        float remaining = length - (_decayPerSecond * elapsedSeconds);

        if (remaining <= _antiDeadZone + RoundsToZero || !(length > 0f))
        {
            Reset();

            return;
        }

        float scale = remaining / length;
        X *= scale;
        Y *= scale;
    }
}
