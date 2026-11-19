// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// Wires the Lossless shaders into the frame-generation graph described in docs/architecture/lsfg.md: per real
/// frame the luma pyramid, alpha on each of its levels and beta over the last three frames; per generated frame
/// gamma from the coarsest level to the finest, delta on the finest three (<see cref="LsfgFlowBuilder"/>), then
/// generate. Textures come from the arena; their counts and formats come from the reflected shaders.
/// </summary>
internal sealed class LsfgGraphBuilder
{
    /// <summary>The real frames kept: the previous and the current.</summary>
    public const int FrameSlots = 2;

    /// <summary>The generated frames per pair at most (multiplier 4).</summary>
    public const int MaxGenerated = LsfgSettings.MaximumMultiplier - 1;

    // Which alpha steps halve the size: the first and third sample their input at twice the thread position.
    private static readonly bool[] AlphaHalves = [true, false, true, false];

    private readonly LsfgShaderSet _set;
    private readonly TextureArena _arena;
    private readonly LsfgPassFactory _passes;
    private readonly LsfgGraph _graph;
    private readonly LsfgWiring _wiring;
    private readonly GpuTexture[] _mips = new GpuTexture[LsfgShapes.Levels];
    private readonly GpuTexture[][][] _features = new GpuTexture[LsfgShapes.RingFrames][][];
    private GpuTexture[] _masks = [];

    public LsfgGraphBuilder(LsfgShaderSet set, TextureArena arena, LsfgGraph graph, LsfgWiring wiring)
    {
        _set = set;
        _arena = arena;
        _graph = graph;
        _wiring = wiring;
        _passes = new LsfgPassFactory(set);
    }

    /// <summary>The flow's size for a frame size and flow scale: the pyramid's largest level.</summary>
    public static (int Width, int Height) FlowSize(int width, int height, float scale)
    {
        return (Math.Max(1, (int)MathF.Round(width * scale)), Math.Max(1, (int)MathF.Round(height * scale)));
    }

    /// <summary>The whole graph for frames of the given size and format; the graph's buffers and flow size are set.</summary>
    public void Build(int width, int height, uint frameFormat)
    {
        for (var slot = 0; slot < FrameSlots; slot++)
        {
            _graph.Frames[slot] = _arena.Create(width, height, frameFormat, writable: false);
        }

        for (var index = 0; index < MaxGenerated; index++)
        {
            _graph.Outputs[index] = _arena.Create(width, height, Direct3D11Values.Rgba8Format, writable: true);
        }

        BuildPyramid();
        BuildAlpha();
        BuildBeta();

        var flow = new LsfgFlowBuilder(_set, _arena, _passes, _graph, _wiring.MaskShift);
        var masks = _masks;

        if (_wiring.WithoutMask)
        {
            masks = [_arena.Create(1, 1, Direct3D11Values.R8Format, writable: false)];
        }

        var (flows, flows2, weights) = flow.Build(_features, masks);
        BuildGenerate(width, height, flows, flows2, weights);
    }

    private void BuildPyramid()
    {
        var width = _graph.FlowWidth;
        var height = _graph.FlowHeight;

        for (var level = 0; level < LsfgShapes.Levels; level++)
        {
            _mips[level] = _arena.Create(width, height, Direct3D11Values.R8Format, writable: true);
            width = LsfgPassFactory.Half(width);
            height = LsfgPassFactory.Half(height);
        }

        // One thread per 2x2 block of the largest level: the shader halves its own work.
        var threadsX = LsfgPassFactory.Half(_graph.FlowWidth);
        var threadsY = LsfgPassFactory.Half(_graph.FlowHeight);

        for (var slot = 0; slot < FrameSlots; slot++)
        {
            var input = LsfgPassFactory.Views([_graph.Frames[slot]]);
            _graph.Mipmaps[slot] = _passes.Pass(LsfgShaderIds.Mipmaps, input, _mips, _graph.Constants, threadsX, threadsY);
        }
    }

    // Alpha on every pyramid level of the current frame, into the ring slot's features; its temporaries are shared.
    private void BuildAlpha()
    {
        var temporaries = new GpuTexture[LsfgShapes.Levels][][];

        for (var slot = 0; slot < LsfgShapes.RingFrames; slot++)
        {
            _features[slot] = new GpuTexture[LsfgShapes.Levels][];
        }

        for (var level = 0; level < LsfgShapes.Levels; level++)
        {
            temporaries[level] = AlphaTemporaries(level);
        }

        for (var slot = 0; slot < LsfgShapes.RingFrames; slot++)
        {
            var passes = new List<ComputePass>();

            for (var level = 0; level < LsfgShapes.Levels; level++)
            {
                var steps = temporaries[level];
                passes.Add(_passes.Pass(LsfgShaderIds.AlphaFirst, LsfgPassFactory.Views([_mips[level]]), steps[0], _graph.Constants));

                for (var step = 1; step < LsfgShaderIds.AlphaSteps; step++)
                {
                    var outputs = step == steps.Length ? _features[slot][level] : steps[step];
                    passes.Add(_passes.Pass(LsfgShaderIds.AlphaFirst + step, LsfgPassFactory.Views(steps[step - 1]), outputs, _graph.Constants));
                }
            }

            _graph.Alpha[slot] = [.. passes];
        }
    }

    // The outputs of each alpha step on one level but the last, which writes each ring slot's features (made here).
    private GpuTexture[][] AlphaTemporaries(int level)
    {
        var last = LsfgShaderIds.AlphaSteps - 1;
        var steps = new GpuTexture[last][];
        var width = _mips[level].Width;
        var height = _mips[level].Height;

        for (var step = 0; step < LsfgShaderIds.AlphaSteps; step++)
        {
            if (AlphaHalves[step])
            {
                width = LsfgPassFactory.Half(width);
                height = LsfgPassFactory.Half(height);
            }

            if (step < last)
            {
                steps[step] = _arena.Outputs(_set.Layout(LsfgShaderIds.AlphaFirst + step), width, height);

                continue;
            }

            for (var slot = 0; slot < LsfgShapes.RingFrames; slot++)
            {
                _features[slot][level] = _arena.Outputs(_set.Layout(LsfgShaderIds.AlphaFirst + step), width, height);
            }
        }

        return steps;
    }

    // Beta over the finest level's features of the three ring frames, oldest first, ending in the UI mask levels.
    private void BuildBeta()
    {
        var size = _features[0][0][0];
        var steps = new GpuTexture[LsfgShaderIds.BetaSteps][];

        for (var step = 0; step < LsfgShaderIds.BetaSteps - 1; step++)
        {
            steps[step] = _arena.Outputs(_set.Layout(LsfgShaderIds.BetaFirst + step), size.Width, size.Height);
        }

        _masks = MaskLevels(size.Width, size.Height);
        steps[^1] = _masks;

        for (var phase = 0; phase < LsfgShapes.RingFrames; phase++)
        {
            var oldest = _features[(phase + 1) % LsfgShapes.RingFrames][0];
            var middle = _features[(phase + LsfgShapes.RingFrames - 1) % LsfgShapes.RingFrames][0];
            var inputs = LsfgPassFactory.Views(oldest, middle, _features[phase][0]);

            if (_wiring.BetaNewestFirst)
            {
                inputs = LsfgPassFactory.Views(_features[phase][0], middle, oldest);
            }

            var passes = new ComputePass[LsfgShaderIds.BetaSteps];
            passes[0] = _passes.Pass(LsfgShaderIds.BetaFirst, inputs, steps[0], _graph.Constants);

            for (var step = 1; step < LsfgShaderIds.BetaSteps; step++)
            {
                passes[step] = _passes.Pass(LsfgShaderIds.BetaFirst + step, LsfgPassFactory.Views(steps[step - 1]), steps[step], _graph.Constants);
            }

            _graph.Beta[phase] = passes;
        }
    }

    private GpuTexture[] MaskLevels(int width, int height)
    {
        var masks = new GpuTexture[LsfgShapes.MaskLevels];

        for (var level = 0; level < masks.Length; level++)
        {
            masks[level] = _arena.Create(width, height, Direct3D11Values.R8Format, writable: true);
            width = LsfgPassFactory.Half(width);
            height = LsfgPassFactory.Half(height);
        }

        return masks;
    }

    // Generate reads the previous frame, the current frame, the finest flow, second flow and weights.
    private void BuildGenerate(int width, int height, GpuTexture flow, GpuTexture flow2, GpuTexture weights)
    {
        for (var parity = 0; parity < FrameSlots; parity++)
        {
            var previous = _graph.Frames[(parity + 1) % FrameSlots];
            var inputs = LsfgPassFactory.Views([previous, _graph.Frames[parity], flow, flow2, weights]);
            _graph.Generate[parity] = new ComputePass[MaxGenerated];

            for (var index = 0; index < MaxGenerated; index++)
            {
                _graph.Generate[parity][index] = _passes.Pass(LsfgShaderIds.Generate, inputs, [_graph.Outputs[index]], _graph.Constants, width, height);
            }
        }
    }
}
