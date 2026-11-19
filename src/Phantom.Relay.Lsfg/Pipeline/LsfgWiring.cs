// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The choices of the graph that no reflection settles (docs/architecture/lsfg.md, "Still hypotheses"), so a live
/// harness can measure the alternatives: the order beta reads the three frames in, which UI-mask level goes with each
/// flow level (0: the level of the same size; 1: the next coarser), and the samplers' filter; and, to see what the mask
/// does at all, a flow that reads none (a blank texture in its place).
/// </summary>
internal sealed record LsfgWiring(bool BetaNewestFirst, int MaskShift, bool PointSampling, bool WithoutMask = false)
{
    /// <summary>
    /// Each flow level reads the UI mask two levels finer than its own size (the finest for the finest two): measured
    /// to narrow the halo of wrongly held background around a still foreground by 14 to 19 % against the mask of the
    /// same size, the foreground still exact and the background no worse (LsfgArtifactLiveTests, docs/architecture/lsfg.md).
    /// </summary>
    public const int FinerMask = -2;

    /// <summary>The wiring the stream runs: beta oldest first, the mask two levels finer, bilinear samplers.</summary>
    public static LsfgWiring Standard { get; } = new(BetaNewestFirst: false, MaskShift: FinerMask, PointSampling: false);
}
