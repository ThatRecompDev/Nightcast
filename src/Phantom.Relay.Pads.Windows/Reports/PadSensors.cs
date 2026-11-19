// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// The motion sensors as both pads send them: the gyroscope's x, y and z, then (elsewhere) the accelerometer's, each a
/// little-endian signed 16-bit count.
/// </summary>
public static class PadSensors
{
    private const int X = 0;
    private const int Y = sizeof(short);
    private const int Z = 2 * sizeof(short);

    /// <summary>The motion from the sensors' offsets in <paramref name="block"/> and the pad's sensor time.</summary>
    public static PadMotion Read(ReadOnlySpan<byte> block, int gyro, int accelerometer, uint sensorTime)
    {
        var turn = block[gyro..];
        var pull = block[accelerometer..];

        return new PadMotion(
            BinaryPrimitives.ReadInt16LittleEndian(turn[X..]),
            BinaryPrimitives.ReadInt16LittleEndian(turn[Y..]),
            BinaryPrimitives.ReadInt16LittleEndian(turn[Z..]),
            BinaryPrimitives.ReadInt16LittleEndian(pull[X..]),
            BinaryPrimitives.ReadInt16LittleEndian(pull[Y..]),
            BinaryPrimitives.ReadInt16LittleEndian(pull[Z..]),
            sensorTime,
            0);
    }
}
