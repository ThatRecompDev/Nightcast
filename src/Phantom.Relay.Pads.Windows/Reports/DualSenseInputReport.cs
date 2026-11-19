// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Numerics;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The DualSense's (and the DualSense Edge's) full input report. Over USB it is report 0x01, 64 bytes; over Bluetooth
/// report 0x31, 78 bytes, once the pad has been switched to it (<see cref="BluetoothModeFeatureReport"/>), with a
/// sequence tag before the shared block and a CRC-32 after it. The shared block, from its first byte:
/// <code>
/// 0 left x | 1 left y | 2 right x | 3 right y | 4 L2 | 5 R2 | 6 counter | 7-9 buttons (PlayStationButtons) | 10 more
/// buttons (Edge) | 11-14 reserved | 15-20 gyro x, y, z (s16) | 21-26 accelerometer x, y, z (s16) | 27-30 sensor time
/// (u32) | ... touch points, battery
/// </code>
/// Every value is little-endian. The layout is the one the public HID descriptors and the Linux hid-playstation
/// documentation describe.
/// </summary>
public static class DualSenseInputReport
{
    public const byte UsbReportId = 0x01;
    public const byte BluetoothReportId = 0x31;

    /// <summary>The feature report whose read switches a pad on Bluetooth from its short report to report 0x31.</summary>
    public const byte BluetoothModeFeatureReport = 0x05;

    /// <summary>What a DualSense offers through the engine's feedback.</summary>
    public const GamepadFeatures Features = GamepadFeatures.Rumble | GamepadFeatures.Light | GamepadFeatures.Touchpad;

    /// <summary>The shared block starts after the report id over USB, and after the id and a sequence tag over Bluetooth.</summary>
    private const int UsbStart = 1;
    private const int BluetoothStart = 2;

    private const int LeftX = 0;
    private const int LeftY = 1;
    private const int RightX = 2;
    private const int RightY = 3;
    private const int LeftTrigger = 4;
    private const int RightTrigger = 5;
    private const int Face = 7;
    private const int Shoulder = 8;
    private const int System = 9;
    private const int Gyro = 15;
    private const int Accelerometer = 21;
    private const int SensorTime = 27;

    /// <summary>The bytes of the shared block read here: through the sensor time.</summary>
    private const int BlockLength = 31;

    /// <summary>
    /// Reads a full report: the pad's state and its motion. False for another report (the short one a Bluetooth pad
    /// sends before it is switched, an audio or status report) or one too short.
    /// </summary>
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
        motion = Motion(block);

        return true;
    }

    private static GamepadState State(ReadOnlySpan<byte> block)
    {
        var buttons = PlayStationButtons.Map(block[Face], block[Shoulder], block[System], hasMute: true);
        var left = Vector2.Create(PadAxes.Stick(block[LeftX]), PadAxes.Stick(block[LeftY]));
        var right = Vector2.Create(PadAxes.Stick(block[RightX]), PadAxes.Stick(block[RightY]));

        return new GamepadState(true, buttons, left, right, PadAxes.Trigger(block[LeftTrigger]), PadAxes.Trigger(block[RightTrigger]), Features);
    }

    private static PadMotion Motion(ReadOnlySpan<byte> block)
    {
        var time = BinaryPrimitives.ReadUInt32LittleEndian(block[SensorTime..]);

        return PadSensors.Read(block, Gyro, Accelerometer, time);
    }
}
