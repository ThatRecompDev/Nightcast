// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg;

/// <summary>
/// The frame generation's settings. Off by default; when on, each pair of real frames gets
/// <see cref="Multiplier"/> - 1 generated frames between them. <see cref="LibraryPath"/> names the user's own
/// Lossless.dll; left empty, it is looked for in the Steam libraries (<see cref="LosslessLocator"/>).
/// </summary>
public sealed record LsfgSettings
{
    /// <summary>The smallest multiplier: one generated frame per real frame (30 fps shown as 60).</summary>
    public const int MinimumMultiplier = 2;

    /// <summary>The largest multiplier: three generated frames per real frame.</summary>
    public const int MaximumMultiplier = 4;

    /// <summary>The smallest flow scale: motion estimated on a quarter-width picture, the fastest.</summary>
    public const float MinimumFlowScale = 0.25f;

    /// <summary>The largest flow scale: motion estimated on the full picture, the default.</summary>
    public const float MaximumFlowScale = 1f;

    /// <summary>The settings a fresh install starts with: off, x2, full flow scale, quality shaders, path found.</summary>
    public static LsfgSettings Default { get; } = new();

    public bool Enabled { get; init; }

    /// <summary>Real plus generated frames per real frame, <see cref="MinimumMultiplier"/> to <see cref="MaximumMultiplier"/>.</summary>
    public int Multiplier { get; init; } = MinimumMultiplier;

    /// <summary>The fraction of the picture's size motion is estimated at; lower is faster and coarser.</summary>
    public float FlowScale { get; init; } = MaximumFlowScale;

    /// <summary>Lossless Scaling's lighter shader set: about half the features per pass.</summary>
    public bool PerformanceMode { get; init; }

    /// <summary>The user's own Lossless.dll; null or empty to find it in the Steam libraries.</summary>
    public string? LibraryPath { get; init; }

    /// <summary>These settings with the multiplier and flow scale brought into range.</summary>
    public LsfgSettings Normalized()
    {
        var multiplier = Math.Clamp(Multiplier, MinimumMultiplier, MaximumMultiplier);
        var scale = float.IsFinite(FlowScale) ? Math.Clamp(FlowScale, MinimumFlowScale, MaximumFlowScale) : MaximumFlowScale;

        return this with { Multiplier = multiplier, FlowScale = scale };
    }
}
