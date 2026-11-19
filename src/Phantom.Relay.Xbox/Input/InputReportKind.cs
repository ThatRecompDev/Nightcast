// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Input;

/// <summary>The report kinds of the input channel (protocol "1.0"), as flags in a report's first field.</summary>
[Flags]
public enum InputReportKind : ushort
{
    None = 0,
    Metadata = 1 << 0,
    Gamepad = 1 << 1,
    Touch = 1 << 2,
    ClientMetadata = 1 << 3,
    ServerMetadata = 1 << 4,
    Mouse = 1 << 5,
    Keyboard = 1 << 6,
    Vibration = 1 << 7,
}
