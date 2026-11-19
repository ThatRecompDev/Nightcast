// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>
/// Frame generation for a game: the mode (off, or Lossless Scaling's quality or performance shaders), how many frames
/// are shown per new picture (x2: a 30 fps game at 60; up to x4), the rate they are shown at (0: the display's own) and
/// the fraction of the picture's size motion is estimated at (lower is faster and coarser). Off by default; it costs
/// about one source frame of latency by its nature (docs/architecture/lsfg.md).
/// </summary>
public sealed record FrameGeneration(FrameGenerationMode Mode, int Multiplier, int TargetHz, float FlowScale = FrameGeneration.FullFlow)
{
    /// <summary>One generated frame per new picture: x2.</summary>
    public const int Doubled = 2;

    /// <summary>The most frames shown per new picture: x4.</summary>
    public const int MaximumMultiplier = 4;

    public const int DisplayRate = 0;

    /// <summary>Motion estimated on the whole picture.</summary>
    public const float FullFlow = 1f;

    /// <summary>The coarsest flow: a quarter of the picture's size.</summary>
    public const float MinimumFlow = 0.25f;

    public static FrameGeneration Off { get; } = new(FrameGenerationMode.Off, 1, DisplayRate);

    /// <summary>x2 with the quality shaders on the whole picture: a 30 fps game shown at 60.</summary>
    public static FrameGeneration QualityDoubled { get; } = new(FrameGenerationMode.Quality, Doubled, DisplayRate);

    /// <summary>Whether frames are generated at all: a mode, and more than one frame per picture.</summary>
    public bool IsOn => Mode != FrameGenerationMode.Off && Multiplier >= Doubled;

    /// <summary>The multiplier brought into x2 to x4.</summary>
    public int ClampedMultiplier => Math.Clamp(Multiplier, Doubled, MaximumMultiplier);

    /// <summary>The flow scale brought into a quarter to the whole picture.</summary>
    public float ClampedFlowScale
    {
        get
        {
            if (!float.IsFinite(FlowScale))
            {
                return FullFlow;
            }

            return Math.Clamp(FlowScale, MinimumFlow, FullFlow);
        }
    }
}
