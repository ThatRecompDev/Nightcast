// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The upscaler and its strength: for LS1 which of its five networks (<see cref="Level"/>, 0 to 4, measured to add
/// more detail as it rises), for SGSR its edge sharpness (<see cref="Sharpness"/>, 0 to 10 as Lossless Scaling's
/// slider, the shader using 1 + 0.2 x it). <see cref="LibraryPath"/> is Lossless.dll's, empty to find it through Steam.
/// </summary>
public sealed record LosslessUpscalerSettings(LosslessUpscalerKind Kind, int Level, int Sharpness, string LibraryPath)
{
    public const int MinimumLevel = 0;
    public const int MaximumLevel = 4;

    /// <summary>The network LS1 runs unless told otherwise (measured: the middle one; docs/architecture/lossless-upscalers.md).</summary>
    public const int DefaultLevel = 2;

    public const int MinimumSharpness = 0;
    public const int MaximumSharpness = 10;

    /// <summary>Lossless Scaling's own default, which gives SGSR its published edge sharpness of 2.</summary>
    public const int DefaultSharpness = 5;

    public static LosslessUpscalerSettings Default { get; } = new(LosslessUpscalerKind.Ls1, DefaultLevel, DefaultSharpness, string.Empty);

    /// <summary>The same settings with every value in range.</summary>
    public LosslessUpscalerSettings Normalized()
    {
        return this with
        {
            Level = Math.Clamp(Level, MinimumLevel, MaximumLevel),
            Sharpness = Math.Clamp(Sharpness, MinimumSharpness, MaximumSharpness),
            LibraryPath = LibraryPath ?? string.Empty,
        };
    }
}
