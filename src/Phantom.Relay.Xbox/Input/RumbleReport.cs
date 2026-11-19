// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Input;

/// <summary>A rumble the console asks for: motor strengths 0..1 and how long it lasts.</summary>
public readonly record struct RumbleReport(
    byte PadIndex,
    float LowFrequency,
    float HighFrequency,
    float LeftTrigger,
    float RightTrigger,
    TimeSpan Duration);
