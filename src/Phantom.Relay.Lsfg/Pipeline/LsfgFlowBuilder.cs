// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The passes of one generated frame before generate: gamma on every pyramid level from the coarsest (a correlation
/// of both frames' features, then its chain to the level's flow), and delta on the finest three levels (a second
/// flow candidate through the correlation and delta's flow chain, and the blend weights through the blend
/// correlation and chain). Each level starts from the coarser level's result; the coarsest from none (FirstIter),
/// delta's weights from none on its first iteration (FirstIterS). Which input is which is documented, and partly a
/// hypothesis, in docs/architecture/lsfg.md.
/// </summary>
internal sealed class LsfgFlowBuilder
{
    // Delta runs on the finest levels only (public descriptions: "delta 0-2 after gamma 4-6").
    private const int DeltaIterations = 3;
    private const int FirstDeltaLevel = LsfgShapes.Levels - DeltaIterations;

    private readonly LsfgShaderSet _set;
    private readonly TextureArena _arena;
    private readonly LsfgPassFactory _passes;
    private readonly LsfgGraph _graph;
    private readonly int _maskShift;
    private readonly LsfgLevel[] _gamma = new LsfgLevel[LsfgShapes.Levels];
    private readonly LsfgLevel[] _deltaFlow = new LsfgLevel[DeltaIterations];
    private readonly LsfgLevel[] _deltaBlend = new LsfgLevel[DeltaIterations];
    private GpuTexture[][][] _features = [];
    private GpuTexture[] _masks = [];
    private GpuTexture _unused = null!;

    public LsfgFlowBuilder(LsfgShaderSet set, TextureArena arena, LsfgPassFactory passes, LsfgGraph graph, int maskShift)
    {
        _set = set;
        _arena = arena;
        _passes = passes;
        _graph = graph;
        _maskShift = maskShift;
    }

    /// <summary>Builds the flow passes of every ring phase; returns what generate reads.</summary>
    public (GpuTexture Flow, GpuTexture Flow2, GpuTexture Weights) Build(GpuTexture[][][] features, GpuTexture[] masks)
    {
        _features = features;
        _masks = masks;

        // Bound where a shader declares an input it skips on a first iteration; never sampled.
        _unused = _arena.Create(1, 1, Direct3D11Values.Rgba16FloatFormat, writable: false);

        for (var level = 0; level < LsfgShapes.Levels; level++)
        {
            _gamma[level] = Allocate(level, LsfgShaderIds.Correlation, LsfgShaderIds.GammaFirst, LsfgShaderIds.GammaSteps);
        }

        for (var iteration = 0; iteration < DeltaIterations; iteration++)
        {
            _deltaFlow[iteration] = Allocate(FirstDeltaLevel + iteration, LsfgShaderIds.Correlation, LsfgShaderIds.DeltaFlowFirst, LsfgShaderIds.DeltaFlowSteps);
            _deltaBlend[iteration] = Allocate(FirstDeltaLevel + iteration, LsfgShaderIds.BlendCorrelation, LsfgShaderIds.BlendFirst, LsfgShaderIds.BlendSteps);
        }

        for (var phase = 0; phase < LsfgShapes.RingFrames; phase++)
        {
            var passes = new List<ComputePass>();
            AddGamma(passes, phase);
            AddDelta(passes, phase);
            _graph.Flow[phase] = [.. passes];
        }

        return (_gamma[^1].Result, _deltaFlow[^1].Result, _deltaBlend[^1].Result);
    }

    // The pyramid level a flow level works on: flow level 0 is the coarsest, on the last pyramid level's features.
    private static int PyramidLevel(int level)
    {
        return LsfgShapes.Levels - 1 - level;
    }

    private LsfgLevel Allocate(int level, int correlation, int first, int steps)
    {
        var size = _features[0][PyramidLevel(level)][0];
        var correlations = _arena.Outputs(_set.Layout(correlation), size.Width, size.Height);
        var temporaries = new GpuTexture[steps - 1][];

        for (var step = 0; step < steps - 1; step++)
        {
            temporaries[step] = _arena.Outputs(_set.Layout(first + step), size.Width, size.Height);
        }

        var result = _arena.Outputs(_set.Layout(first + steps - 1), size.Width, size.Height, signed: true)[0];

        return new LsfgLevel(correlations, temporaries, result);
    }

    private void AddGamma(List<ComputePass> passes, int phase)
    {
        for (var level = 0; level < LsfgShapes.Levels; level++)
        {
            var constants = level == 0 ? _graph.FirstConstants : _graph.Constants;
            var prior = level == 0 ? _unused : _gamma[level - 1].Result;
            var gamma = _gamma[level];
            var features = Features(phase, level);
            passes.Add(_passes.Pass(LsfgShaderIds.Correlation, LsfgPassFactory.Views(features, [prior]), gamma.Correlations, constants));
            Chain(passes, LsfgShaderIds.GammaFirst, gamma, [prior, Mask(level)], constants);
        }
    }

    private void AddDelta(List<ComputePass> passes, int phase)
    {
        for (var iteration = 0; iteration < DeltaIterations; iteration++)
        {
            var level = FirstDeltaLevel + iteration;
            var first = iteration == 0;
            var constants = first ? _graph.DeltaFirstConstants : _graph.Constants;
            var coarser = _gamma[level - 1].Result;
            var prior = first ? coarser : _deltaFlow[iteration - 1].Result;
            var previousFlow2 = first ? _unused : _deltaFlow[iteration - 1].Result;

            var priorWeights = first ? _unused : _deltaBlend[iteration - 1].Result;
            var features = Features(phase, level);
            var flow = _deltaFlow[iteration];
            var blend = _deltaBlend[iteration];
            passes.Add(_passes.Pass(LsfgShaderIds.Correlation, LsfgPassFactory.Views(features, [prior]), flow.Correlations, constants));
            Chain(passes, LsfgShaderIds.DeltaFlowFirst, flow, [prior, Mask(level)], constants);

            passes.Add(_passes.Pass(LsfgShaderIds.BlendCorrelation, LsfgPassFactory.Views(features, [coarser, previousFlow2]), blend.Correlations, constants));
            Chain(passes, LsfgShaderIds.BlendFirst, blend, [priorWeights], constants);
        }
    }

    // A chain after its correlation: each step reads the previous step's outputs; the last also reads the extras.
    private void Chain(List<ComputePass> passes, int first, LsfgLevel level, GpuTexture[] extras, nint constants)
    {
        var previous = level.Correlations;
        var steps = level.Temporaries.Length + 1;

        for (var step = 0; step < steps; step++)
        {
            var last = step == steps - 1;
            GpuTexture[] outputs = last ? [level.Result] : level.Temporaries[step];
            var inputs = last ? LsfgPassFactory.Views(previous, extras) : LsfgPassFactory.Views(previous);
            passes.Add(_passes.Pass(first + step, inputs, outputs, constants));
            previous = outputs;
        }
    }

    // The previous frame's features then the current frame's, at the flow level's pyramid level.
    private GpuTexture[] Features(int phase, int level)
    {
        var pyramid = PyramidLevel(level);
        var previous = _features[(phase + LsfgShapes.RingFrames - 1) % LsfgShapes.RingFrames][pyramid];
        var current = _features[phase][pyramid];

        return [.. previous, .. current];
    }

    // The UI mask level shifted (LsfgWiring.MaskShift; the stream's: two levels finer) from the one of the flow level's
    // size (beta's levels start at the finest features' size), within the levels there are.
    private GpuTexture Mask(int level)
    {
        return _masks[Math.Clamp(PyramidLevel(level) + _maskShift, 0, _masks.Length - 1)];
    }
}
