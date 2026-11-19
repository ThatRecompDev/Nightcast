// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// A pad's motion sensors as its last report gave them, for gyro aiming: the gyroscope's and the accelerometer's three
/// axes as the raw signed counts the pad sends, without the factory calibration (feature report 0x05 on a DualSense,
/// 0x02 on a DualShock 4) applied, and the pad's own sensor clock (a DualSense counts in 1/3 microseconds over 32
/// bits, a DualShock 4 in about 5.33 microseconds over 16 bits). <see cref="Sequence"/> grows by one with every
/// report read, so a reader can tell a new sample from the one it already used.
/// </summary>
public readonly record struct PadMotion(
    short GyroX,
    short GyroY,
    short GyroZ,
    short AccelerometerX,
    short AccelerometerY,
    short AccelerometerZ,
    uint SensorTime,
    uint Sequence);
