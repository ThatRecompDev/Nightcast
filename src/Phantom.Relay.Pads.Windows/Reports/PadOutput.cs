// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// What an output report sets: the motors (the low-frequency one is the heavy left motor, the high-frequency one the
/// light right motor, 0 to 255) and, when <see cref="Light"/> is set, the light bar's colour. A report without
/// <see cref="Light"/> leaves the light as it was. <see cref="ReleaseLight"/> also ends a DualSense's own light-bar
/// animation, which it plays from connection until a host takes the light over.
/// </summary>
public readonly record struct PadOutput(byte LowMotor, byte HighMotor, bool Light, byte Red, byte Green, byte Blue, bool ReleaseLight);
