// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.Xbox.Input;

/// <summary>
/// Reads the console's vibration reports from the input channel (docs/architecture/xhome.md#input-reports):
/// <code>
/// u16 kind (128) | u8 rumble type (0, four motors) | u8 pad | u8 low % | u8 high % | u8 left trigger %
/// | u8 right trigger % | u16 duration ms | u16 delay ms | u8 repeat                             13 bytes
/// </code>
/// From input protocol 8 the kind is two bytes, as in the client's reports, and the console's reports carry no
/// sequence or time (seen live 2026-11-26).
/// </summary>
public static class RumbleReportReader
{
    public const int ReportSize = 13;

    private const int TypeOffset = 2;
    private const int PadOffset = 3;
    private const int LowOffset = 4;
    private const int HighOffset = 5;
    private const int LeftTriggerOffset = 6;
    private const int RightTriggerOffset = 7;
    private const int DurationOffset = 8;
    private const byte FourMotors = 0;
    private const float PercentScale = 100f;

    [HotPath]
    public static bool TryRead(ReadOnlySpan<byte> data, out RumbleReport report)
    {
        report = default;

        if (data.Length < ReportSize
            || BinaryPrimitives.ReadUInt16LittleEndian(data) != (ushort)InputReportKind.Vibration
            || data[TypeOffset] != FourMotors)
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
