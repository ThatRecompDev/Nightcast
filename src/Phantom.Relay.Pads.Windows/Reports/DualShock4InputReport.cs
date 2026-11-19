// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Numerics;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The DualShock 4's full input report. Over USB it is report 0x01, 64 bytes; over Bluetooth report 0x11, 78 bytes,
/// once the pad has been switched to it (<see cref="BluetoothModeFeatureReport"/>), with two bytes before the shared
/// block and a CRC-32 at the end. The shared block, from its first byte:
/// <code>
/// 0 left x | 1 left y | 2 right x | 3 right y | 4-6 buttons (PlayStationButtons) | 7 L2 | 8 R2 | 9-10 sensor time
/// (u16) | 11 temperature | 12-17 gyro x, y, z (s16) | 18-23 accelerometer x, y, z (s16) | ... touch points, battery
/// </code>
/// Every value is little-endian. The layout is the one the public HID descriptors and the Linux hid-playstation
/// documentation describe. The DualShock 4 has no mute button: the system byte's high bits count reports.
/// </summary>
public static class DualShock4InputReport
{
    public const byte UsbReportId = 0x01;
    public const byte BluetoothReportId = 0x11;

    /// <summary>The feature report whose read switches a pad on Bluetooth from its short report to report 0x11.</summary>
    public const byte BluetoothModeFeatureReport = 0x02;

    /// <summary>What a DualShock 4 offers through the engine's feedback.</summary>
    public const GamepadFeatures Features = GamepadFeatures.Rumble | GamepadFeatures.Light | GamepadFeatures.Touchpad;

    /// <summary>The shared block starts after the report id over USB, and after the id and two more bytes over Bluetooth.</summary>
    private const int UsbStart = 1;
    private const int BluetoothStart = 3;

    private const int LeftX = 0;
    private const int LeftY = 1;
    private const int RightX = 2;
    private const int RightY = 3;
    private const int Face = 4;
    private const int Shoulder = 5;
    private const int System = 6;
    private const int LeftTrigger = 7;
    private const int RightTrigger = 8;
    private const int SensorTime = 9;
    private const int Gyro = 12;
    private const int Accelerometer = 18;

    /// <summary>The bytes of the shared block read here: through the accelerometer.</summary>
    private const int BlockLength = 24;

    /// <summary>Reads a full report: the pad's state and its motion. False for another report or one too short.</summary>
    public static bool TryRead(ReadOnlySpan<byte> report, PadTransport transport, out GamepadState state, out PadMotion motion)
    {
        state = default;
        motion = default;
        byte id = transport == PadTransport.Usb ? UsbReportId : BluetoothReportId;
        int start = transport == PadTransport.Usb ? UsbStart : BluetoothStart;

        if (report.Length < start + BlockLength || report[0] != id)
        {
            return false;
        }

        var block = report.Slice(start, BlockLength);
        state = State(block);
        motion = PadSensors.Read(block, Gyro, Accelerometer, BinaryPrimitives.ReadUInt16LittleEndian(block[SensorTime..]));

        return true;
    }

    private static GamepadState State(ReadOnlySpan<byte> block)
    {
        var buttons = PlayStationButtons.Map(block[Face], block[Shoulder], block[System], hasMute: false);
        var left = Vector2.Create(PadAxes.Stick(block[LeftX]), PadAxes.Stick(block[LeftY]));
        var right = Vector2.Create(PadAxes.Stick(block[RightX]), PadAxes.Stick(block[RightY]));

        return new GamepadState(true, buttons, left, right, PadAxes.Trigger(block[LeftTrigger]), PadAxes.Trigger(block[RightTrigger]), Features);
    }
}
