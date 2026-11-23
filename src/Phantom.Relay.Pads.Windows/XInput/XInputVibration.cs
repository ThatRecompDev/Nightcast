// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Pads.Windows.XInput;

/// <summary>XInput's XINPUT_VIBRATION: the left (low-frequency) and right (high-frequency) motor speeds, 0 to 65535.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XInputVibration
{
    public ushort LeftMotor;
    public ushort RightMotor;
}
