// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Shaders;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The wiring the pipeline assumes, checked against what D3DReflect reports for the user's DLL: each chain's steps
/// read exactly what the step before wrote, plus the extra inputs named here. Counts are never assumed; they are
/// read from the reflection and only their relations are fixed. A mismatch names the shader and both counts.
/// </summary>
internal static class LsfgShapes
{
    /// <summary>The luma pyramid's levels: the mipmaps shader's outputs, and so the flow's levels.</summary>
    public const int Levels = 7;

    /// <summary>The UI mask's levels: the beta chain's last step's outputs.</summary>
    public const int MaskLevels = 6;

    /// <summary>The frames beta reads features of (the current one and the two before it).</summary>
    public const int RingFrames = 3;

    /// <summary>The frames a correlation compares: the previous and the current.</summary>
    public const int FramesCompared = 2;

    /// <summary>The correlation's extra input: the prior flow, upsampled from the coarser level.</summary>
    public const int CorrelationExtras = 1;

    /// <summary>A flow chain's last step's extra inputs: the prior flow and the UI mask.</summary>
    public const int FlowExtras = 2;

    /// <summary>The blend correlation's extra inputs: the coarser level's flow and the previous second flow.</summary>
    public const int BlendCorrelationExtras = 2;

    /// <summary>The blend chain's last step's extra input: the previous iteration's weights.</summary>
    public const int BlendExtras = 1;

    /// <summary>The generate shader's inputs: previous frame, current frame, flow, second flow, weights.</summary>
    public const int GenerateInputs = 5;

    /// <summary>The first problem with the set's shapes, or null.</summary>
    public static string? Check(LsfgShaderSet set)
    {
        var features = set.Features;
        var maskStep = LsfgShaderIds.BetaFirst + LsfgShaderIds.BetaSteps - 1;

        return Expect(set, LsfgShaderIds.Mipmaps, 1, Levels)
            ?? Expect(set, LsfgShaderIds.Generate, GenerateInputs, 1)
            ?? Chain(set, LsfgShaderIds.AlphaFirst, LsfgShaderIds.AlphaSteps, 1, 0)
            ?? Chain(set, LsfgShaderIds.BetaFirst, LsfgShaderIds.BetaSteps, RingFrames * features, 0)
            ?? Expect(set, maskStep, set.Layout(maskStep - 1).UnorderedAccesses, MaskLevels)
            ?? Expect(set, LsfgShaderIds.Correlation, (FramesCompared * features) + CorrelationExtras, set.Layout(LsfgShaderIds.Correlation).UnorderedAccesses)
            ?? Flow(set, LsfgShaderIds.GammaFirst, LsfgShaderIds.GammaSteps)
            ?? Flow(set, LsfgShaderIds.DeltaFlowFirst, LsfgShaderIds.DeltaFlowSteps)
            ?? Expect(set, LsfgShaderIds.BlendCorrelation, (FramesCompared * features) + BlendCorrelationExtras, set.Layout(LsfgShaderIds.BlendCorrelation).UnorderedAccesses)
            ?? Blend(set);
    }

    // A flow chain reads the correlation's outputs and ends in one flow texture.
    private static string? Flow(LsfgShaderSet set, int first, int steps)
    {
        var correlation = set.Layout(LsfgShaderIds.Correlation).UnorderedAccesses;

        return Chain(set, first, steps, correlation, FlowExtras) ?? Expect(set, first + steps - 1, Inputs(set, first + steps - 1), 1);
    }

    private static string? Blend(LsfgShaderSet set)
    {
        var correlation = set.Layout(LsfgShaderIds.BlendCorrelation).UnorderedAccesses;
        var last = LsfgShaderIds.BlendFirst + LsfgShaderIds.BlendSteps - 1;

        return Chain(set, LsfgShaderIds.BlendFirst, LsfgShaderIds.BlendSteps, correlation, BlendExtras) ?? Expect(set, last, Inputs(set, last), 1);
    }

    // Each step reads the previous step's outputs (the first reads firstInputs); the last also reads lastExtras more.
    private static string? Chain(LsfgShaderSet set, int first, int steps, int firstInputs, int lastExtras)
    {
        var expected = firstInputs;

        for (var step = 0; step < steps; step++)
        {
            var role = first + step;
            var extras = step == steps - 1 ? lastExtras : 0;
            var problem = Expect(set, role, expected + extras, set.Layout(role).UnorderedAccesses);

            if (problem is not null)
            {
                return problem;
            }

            expected = set.Layout(role).UnorderedAccesses;
        }

        return null;
    }

    private static int Inputs(LsfgShaderSet set, int role)
    {
        return set.Layout(role).Textures;
    }

    private static string? Expect(LsfgShaderSet set, int role, int inputs, int outputs)
    {
        ShaderLayout layout = set.Layout(role);

        if (layout.Textures == inputs && layout.UnorderedAccesses == outputs)
        {
            return null;
        }

        return $"shader {set.Id(role)} reads {layout.Textures} textures and writes {layout.UnorderedAccesses}; the pipeline wires {inputs} and {outputs}";
    }
}
