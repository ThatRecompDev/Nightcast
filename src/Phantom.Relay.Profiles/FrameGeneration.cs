// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>
/// Frame generation: the mode, how many frames are shown per new picture (2: a 30 fps game at 60) and the rate they
/// are shown at (0: the display's own).
/// </summary>
public sealed record FrameGeneration(FrameGenerationMode Mode, int Multiplier, int TargetHz)
{
    public const int Doubled = 2;

    public const int DisplayRate = 0;

    public static FrameGeneration Off { get; } = new(FrameGenerationMode.Off, 1, DisplayRate);
}
