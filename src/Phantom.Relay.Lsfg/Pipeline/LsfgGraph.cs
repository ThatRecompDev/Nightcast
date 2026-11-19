// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The built pass lists of the pipeline and the textures the caller sees: the per-real-frame passes (the luma
/// pyramid by frame parity, alpha by ring slot, beta by ring phase), the flow passes of a generated frame by ring
/// phase, and the generate passes by parity and generated index.
/// </summary>
internal sealed class LsfgGraph
{
    public LsfgGraph(int frameSlots, int ringSlots, int maxGenerated)
    {
        Frames = new GpuTexture[frameSlots];
        Mipmaps = new ComputePass[frameSlots];
        Alpha = new ComputePass[ringSlots][];
        Beta = new ComputePass[ringSlots][];

        Flow = new ComputePass[ringSlots][];
        Generate = new ComputePass[frameSlots][];
        Outputs = new GpuTexture[maxGenerated];
    }

    /// <summary>The copies of the last two real frames, by frame parity.</summary>
    public GpuTexture[] Frames { get; }

    /// <summary>The generated frames, by index within a pair.</summary>
    public GpuTexture[] Outputs { get; }

    public ComputePass[] Mipmaps { get; }

    public ComputePass[][] Alpha { get; }

    public ComputePass[][] Beta { get; }

    public ComputePass[][] Flow { get; }

    public ComputePass[][] Generate { get; }

    /// <summary>The constant buffer of gamma's coarsest level (FirstIter set).</summary>
    public nint FirstConstants { get; set; }

    /// <summary>The constant buffer of every other pass (FirstIter and FirstIterS clear).</summary>
    public nint Constants { get; set; }

    /// <summary>The constant buffer of delta's first iteration (FirstIterS set).</summary>
    public nint DeltaFirstConstants { get; set; }

    /// <summary>The sampler bound to both s0 and s1.</summary>
    public nint Sampler { get; set; }

    /// <summary>The flow's size (the pyramid's largest level).</summary>
    public int FlowWidth { get; set; }

    public int FlowHeight { get; set; }

    /// <summary>The inverse flow scale the shaders read (frame width over flow width).</summary>
    public float InverseScale { get; set; }
}
