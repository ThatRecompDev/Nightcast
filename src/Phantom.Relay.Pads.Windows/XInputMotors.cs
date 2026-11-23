// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.Versioning;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Windows.XInput;

namespace Phantom.Relay.Pads.Windows;

/// <summary>
/// The motors of XInput's pads (Xbox pads, a handheld's built-in pad such as the MSI Claw's), by XInput user, which is
/// the engine's slot in its XInput source. The engine's XInput source only reads; this is its feedback side until the
/// engine has one (an engine candidate). XInput pads have no light.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class XInputMotors : IGamepadFeedback
{
    private const float FullSpeed = ushort.MaxValue;

    public bool TrySetRumble(int gamepad, float lowFrequency, float highFrequency)
    {
        if (gamepad < 0 || gamepad >= XInputVibrationMethods.Users)
        {
            return false;
        }

        var vibration = new XInputVibration { LeftMotor = Speed(lowFrequency), RightMotor = Speed(highFrequency) };

        return XInputVibrationMethods.SetState((uint)gamepad, ref vibration) == XInputVibrationMethods.Success;
    }

    public bool TrySetLight(int gamepad, float red, float green, float blue)
    {
        return false;
    }

    private static ushort Speed(float strength)
    {
        return (ushort)(Math.Clamp(strength, 0f, 1f) * FullSpeed);
    }
}
