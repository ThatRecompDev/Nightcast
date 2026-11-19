// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// Keyboard and mouse as the console's pad, one input frame at a time (<see cref="KeyboardMouseSettings"/>): held
/// keys and mouse buttons press their buttons, pull their triggers fully or push their stick directions; wheel
/// notches press for a moment (<see cref="WheelPresses"/>); the movement keys push the left stick, a diagonal
/// normalised to full tilt and the walk key scaling it; the mouse drives the right stick (<see cref="MouseStick"/>).
/// A stick's sources add up and are clamped to the unit circle. The pad is the console's: +Y up. Switched off, it is
/// the pad at rest. Made once per profile; a frame allocates nothing.
/// </summary>
public sealed class KeyboardMouseMapper
{
    private readonly bool _enabled;
    private readonly MovementSettings _movement;
    private readonly KeyBinding[] _keys;
    private readonly MouseButtonBinding[] _mouseButtons;
    private readonly WheelBinding[] _wheelBindings;
    private readonly PointerButtons _aimButton;
    private readonly MouseStick _mouse;
    private readonly WheelPresses _wheel;
    private readonly float[] _directions = new float[PadControls.StickDirections];
    private XboxButtons _buttons;
    private float _leftTrigger;
    private float _rightTrigger;

    public KeyboardMouseMapper(KeyboardMouseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _enabled = settings.Enabled;
        _movement = settings.Movement;
        _keys = [.. settings.Keys];
        _mouseButtons = [.. settings.MouseButtons];
        _wheelBindings = [.. settings.Wheel.Bindings];
        _aimButton = PointerButtonBits.Of(settings.MouseLook.AimButton);

        _mouse = new MouseStick(settings.MouseLook);
        _wheel = new WheelPresses(settings.Wheel.PressSeconds);
    }

    /// <summary>The pad for this input frame, <paramref name="elapsedSeconds"/> after the last.</summary>
    [HotPath]
    public XboxPadState Map(InputFrame frame, float elapsedSeconds)
    {
        if (!_enabled)
        {
            return default;
        }

        Clear();
        ApplyMovement(frame);
        ApplyKeys(frame);
        ApplyMouseButtons(frame.PointerButtons);
        _wheel.Step(frame.Wheel.X, frame.Wheel.Y, elapsedSeconds);
        ApplyWheel();

        bool aiming = (frame.PointerButtons & _aimButton) != 0;
        _mouse.Step(frame.PointerDelta.X, frame.PointerDelta.Y, elapsedSeconds, aiming);

        return Compose(frame.IsKeyDown(_movement.Walk));
    }

    /// <summary>Centres the mouse's stick at once: the window lost the mouse, or the stream paused.</summary>
    public void Reset()
    {
        _mouse.Reset();
    }

    /// <summary>The factor that brings (x, y) back inside the unit circle: 1 when it is inside already.</summary>
    [HotPath]
    private static float UnitScale(float x, float y)
    {
        float length = MathF.Sqrt((x * x) + (y * y));

        if (length > 1f)
        {
            return 1f / length;
        }

        return 1f;
    }

    [HotPath]
    private void Clear()
    {
        _buttons = XboxButtons.None;
        _leftTrigger = 0f;
        _rightTrigger = 0f;
        Array.Clear(_directions);
    }

    [HotPath]
    private void ApplyMovement(InputFrame frame)
    {
        ApplyIfDown(frame, _movement.Forward, PadControl.LeftStickUp);
        ApplyIfDown(frame, _movement.Back, PadControl.LeftStickDown);
        ApplyIfDown(frame, _movement.Left, PadControl.LeftStickLeft);
        ApplyIfDown(frame, _movement.Right, PadControl.LeftStickRight);
        ApplyIfDown(frame, _movement.Sprint, _movement.SprintControl);
    }

    [HotPath]
    private void ApplyKeys(InputFrame frame)
    {
        foreach (var binding in _keys)
        {
            ApplyIfDown(frame, binding.Key, binding.Control);
        }
    }

    [HotPath]
    private void ApplyMouseButtons(PointerButtons held)
    {
        foreach (var binding in _mouseButtons)
        {
            if ((held & PointerButtonBits.Of(binding.Button)) != 0)
            {
                Apply(binding.Control);
            }
        }
    }

    [HotPath]
    private void ApplyWheel()
    {
        foreach (var binding in _wheelBindings)
        {
            if (_wheel.IsPressed(binding.Direction))
            {
                Apply(binding.Control);
            }
        }
    }

    [HotPath]
    private void ApplyIfDown(InputFrame frame, Key key, PadControl control)
    {
        if (frame.IsKeyDown(key))
        {
            Apply(control);
        }
    }

    [HotPath]
    private void Apply(PadControl control)
    {
        if (control == PadControl.LeftTrigger)
        {
            _leftTrigger = 1f;

            return;
        }

        if (control == PadControl.RightTrigger)
        {
            _rightTrigger = 1f;

            return;
        }

        if (PadControls.IsStickDirection(control))
        {
            _directions[PadControls.DirectionIndex(control)] = 1f;

            return;
        }

        _buttons |= PadControls.ButtonOf(control);
    }

    [HotPath]
    private XboxPadState Compose(bool walking)
    {
        // The left stick: a diagonal is full tilt, not 1.41, and walking tilts it part of the way.
        float leftX = Direction(PadControl.LeftStickRight) - Direction(PadControl.LeftStickLeft);
        float leftY = Direction(PadControl.LeftStickUp) - Direction(PadControl.LeftStickDown);
        float leftScale = UnitScale(leftX, leftY);

        if (walking)
        {
            leftScale *= _movement.WalkTilt;
        }

        // The right stick: the mouse and any keys bound to its directions, kept inside the unit circle.
        float rightX = _mouse.X + Direction(PadControl.RightStickRight) - Direction(PadControl.RightStickLeft);
        float rightY = _mouse.Y + Direction(PadControl.RightStickUp) - Direction(PadControl.RightStickDown);
        float rightScale = UnitScale(rightX, rightY);

        return new XboxPadState(
            _buttons,
            leftX * leftScale,
            leftY * leftScale,
            rightX * rightScale,
            rightY * rightScale,
            _leftTrigger,
            _rightTrigger);
    }

    [HotPath]
    private float Direction(PadControl control)
    {
        return _directions[PadControls.DirectionIndex(control)];
    }
}
