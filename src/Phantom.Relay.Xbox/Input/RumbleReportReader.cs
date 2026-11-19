// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.Xbox.Input;

/// <summary>
/// Reads the console's vibration reports from the input channel (docs/architecture/xhome.md#input-reports):
/// <code>
/// u8 kind (128) | u8 rumble type | u8 pad | u8 low % | u8 high % | u8 left trigger % | u8 right trigger %
/// | u16 duration ms | u16 delay ms | u8 repeat                                                  12 bytes
/// </code>
/// The layout was read from the web player and is not yet confirmed against a console.
/// </summary>
public static class RumbleReportReader
{
    public const int ReportSize = 12;

    private const int PadOffset = 2;
    private const int LowOffset = 3;
    private const int HighOffset = 4;
    private const int LeftTriggerOffset = 5;
    private const int RightTriggerOffset = 6;
    private const int DurationOffset = 7;
    private const float PercentScale = 100f;

    [HotPath]
    public static bool TryRead(ReadOnlySpan<byte> data, out RumbleReport report)
    {
        report = default;

        if (data.Length < ReportSize || data[0] != (byte)InputReportKind.Vibration)
        {
            return false;
        }

        ushort milliseconds = BinaryPrimitives.ReadUInt16LittleEndian(data[DurationOffset..]);

        report = new RumbleReport(
            data[PadOffset],
            Percent(data[LowOffset]),
            Percent(data[HighOffset]),
            Percent(data[LeftTriggerOffset]),
            Percent(data[RightTriggerOffset]),
            TimeSpan.FromMilliseconds(milliseconds));

        return true;
    }

    private static float Percent(byte value)
    {
        return Math.Clamp(value / PercentScale, 0f, 1f);
    }
}
