// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The RT_RCDATA ids of the frame-generation shaders in Lossless.dll, for the quality set and, with
/// <see cref="PerformanceOffset"/>, the performance set. A public port's description gave the ranges (mipmaps 255,
/// generate 256, the rest 257-279, performance 280-302); each role below was then found by reflecting the user's
/// DLL with D3DReflect (bindings, thread groups, which constants each reads) and reading how each samples its
/// inputs. docs/architecture/lsfg.md lists the evidence per role and what is still a hypothesis. Every id is checked
/// against its expected shape when the pipeline is built, so a DLL whose ids moved is refused, not misdriven.
/// </summary>
internal static class LsfgShaderIds
{
    /// <summary>
    /// Luma pyramid: one colour texture in, seven single-channel outputs halving from the flow size; 32x32 groups,
    /// each thread writing a 2x2 block of the largest level; reads AdvancedColorKind, HdrSupport, ResolutionInvScale.
    /// </summary>
    public const int Mipmaps = 255;

    /// <summary>
    /// The generated frame: five textures in (the two real frames, two flow textures, the blend weights), one colour
    /// output at the frame's size; 16x16 groups; reads ResolutionInvScale and Timestamp. Shared by both sets.
    /// </summary>
    public const int Generate = 256;

    /// <summary>Alpha, per real frame and pyramid level: four steps, 1 to 2 (half size), 2 to 2, 2 to 4 (half), 4 to 4.</summary>
    public const int AlphaFirst = 267;

    /// <summary>The alpha steps.</summary>
    public const int AlphaSteps = 4;

    /// <summary>
    /// Beta, per real frame: the finest level's alpha features of the last three frames (twelve in) to two, three
    /// two-to-two steps, then a 32x32-group step to six single-channel mask levels that reads UiThreshold.
    /// </summary>
    public const int BetaFirst = 275;

    /// <summary>The beta steps.</summary>
    public const int BetaSteps = 5;

    /// <summary>
    /// Gamma's correlation, per level and generated frame: both frames' features (one set warped by the prior flow
    /// times Timestamp, the other by its second half times 1 - Timestamp, sampled 3x3) and the prior flow in; nine
    /// correlations out (two colour textures and one single value); reads FirstIter and Timestamp. Delta's flow
    /// chain runs it too.
    /// </summary>
    public const int Correlation = 257;

    /// <summary>Gamma after the correlation: 3 to 4, 4 to 4, 4 to 4, then 4 plus prior flow plus mask to the level's flow.</summary>
    public const int GammaFirst = 259;

    /// <summary>The gamma steps after the correlation.</summary>
    public const int GammaSteps = 4;

    /// <summary>Delta's flow chain after its correlation, the same shapes as gamma's: the second flow candidate.</summary>
    public const int DeltaFlowFirst = 263;

    /// <summary>The delta flow steps after the correlation.</summary>
    public const int DeltaFlowSteps = 4;

    /// <summary>
    /// Delta's blend correlation: both frames' features, the coarser level's flow and the previous delta iteration's
    /// second flow (skipped when FirstIterS) in; two outputs.
    /// </summary>
    public const int BlendCorrelation = 258;

    /// <summary>Delta's blend chain after its correlation: three two-to-two steps, then two plus the prior weights to the weights.</summary>
    public const int BlendFirst = 271;

    /// <summary>The delta blend steps after the correlation.</summary>
    public const int BlendSteps = 4;

    /// <summary>The performance set's ids follow the quality set's 257 to 279 one for one, 23 later (same shapes, half the features).</summary>
    public const int PerformanceOffset = 23;

    /// <summary>The id of a role's shader in the chosen set; the mipmaps and generate shaders are shared.</summary>
    public static int Of(int qualityId, bool performance)
    {
        if (!performance || qualityId == Mipmaps || qualityId == Generate)
        {
            return qualityId;
        }

        return qualityId + PerformanceOffset;
    }
}
