// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The built frame-generation graph on one device, and its running: a real frame is copied in and its passes run
/// (<see cref="Submit"/>); each generated frame runs the flow passes and generate with its timestamp
/// (<see cref="Generate"/>). Everything is made once; running allocates nothing.
/// </summary>
internal sealed class LsfgPipeline : IDisposable
{
    private readonly ComputeDevice _device;
    private readonly TextureArena _arena;
    private readonly LsfgGraph _graph;
    private LsfgConstants _values;
    private nint _first;
    private nint _constants;
    private nint _deltaFirst;
    private nint _sampler;
    private long _submitted;

    private LsfgPipeline(ComputeDevice device, TextureArena arena, LsfgGraph graph, LsfgConstants values)
    {
        _device = device;
        _arena = arena;
        _graph = graph;
        _values = values;

        _first = graph.FirstConstants;
        _constants = graph.Constants;
        _deltaFirst = graph.DeltaFirstConstants;
        _sampler = graph.Sampler;
    }

    public int FlowWidth => _graph.FlowWidth;

    public int FlowHeight => _graph.FlowHeight;

    /// <summary>The real frames submitted so far.</summary>
    public long Submitted => _submitted;

    /// <summary>The dispatches each real frame costs.</summary>
    public int PassesPerFrame => 1 + _graph.Alpha[0].Length + _graph.Beta[0].Length;

    /// <summary>The dispatches each generated frame costs.</summary>
    public int PassesPerGenerated => _graph.Flow[0].Length + 1;

    /// <summary>The graph for frames of the given size and format; null and why when the device refuses a part.</summary>
    public static LsfgPipeline? TryBuild(ComputeDevice device, LsfgShaderSet set, LsfgSettings settings, int width, int height, uint format, out string? problem)
    {
        var (flowWidth, flowHeight) = LsfgGraphBuilder.FlowSize(width, height, settings.FlowScale);
        var values = new LsfgConstants
        {
            AdvancedColorKind = LsfgConstantLayout.StandardDynamicRange,
            ResolutionInvScale = width / (float)flowWidth,
            UiThreshold = LsfgConstantLayout.UiThreshold,
        };

        var graph = new LsfgGraph(LsfgGraphBuilder.FrameSlots, LsfgShapes.RingFrames, LsfgGraphBuilder.MaxGenerated)
        {
            FlowWidth = flowWidth,
            FlowHeight = flowHeight,
            Sampler = device.CreateLinearClampSampler(),
        };

        var first = values with { FirstIter = 1 };
        var deltaFirst = values with { FirstIterS = 1 };
        graph.FirstConstants = device.CreateConstantBuffer(LsfgConstantLayout.Size, Bytes(in first));
        graph.Constants = device.CreateConstantBuffer(LsfgConstantLayout.Size, Bytes(in values));
        graph.DeltaFirstConstants = device.CreateConstantBuffer(LsfgConstantLayout.Size, Bytes(in deltaFirst));

        var arena = new TextureArena(device);
        var pipeline = new LsfgPipeline(device, arena, graph, values);

        if (graph.Sampler == 0 || graph.FirstConstants == 0 || graph.Constants == 0 || graph.DeltaFirstConstants == 0)
        {
            problem = "D3D11 refused the pipeline's sampler or constant buffers";
            pipeline.Dispose();

            return null;
        }

        try
        {
            new LsfgGraphBuilder(set, arena, graph).Build(width, height, format);
        }
        catch (InvalidOperationException exception)
        {
            problem = exception.Message;
            pipeline.Dispose();

            return null;
        }

        problem = null;

        return pipeline;
    }

    /// <summary>Takes a real frame (copied, so the caller may reuse its texture) and runs its passes.</summary>
    [HotPath]
    public void Submit(nint frame)
    {
        var parity = (int)(_submitted % LsfgGraphBuilder.FrameSlots);
        var slot = (int)(_submitted % LsfgShapes.RingFrames);
        _device.Copy(_graph.Frames[parity].Texture, frame);
        _device.Run(_graph.Mipmaps[parity], _sampler);
        Run(_graph.Alpha[slot]);

        if (_submitted == 0)
        {
            // The first frame stands in for the two before it, so beta's ring is never read unwritten.
            Run(_graph.Alpha[(slot + 1) % LsfgShapes.RingFrames]);
            Run(_graph.Alpha[(slot + LsfgShapes.RingFrames - 1) % LsfgShapes.RingFrames]);
        }

        Run(_graph.Beta[slot]);
        _submitted++;
    }

    /// <summary>
    /// The frames between the last two real frames, <paramref name="count"/> of them at timestamps
    /// (index + 1) / (count + 1); returns how many were made (none before two real frames).
    /// </summary>
    [HotPath]
    public int Generate(int count)
    {
        if (_submitted < LsfgGraphBuilder.FrameSlots)
        {
            return 0;
        }

        var made = Math.Clamp(count, 0, LsfgGraphBuilder.MaxGenerated);
        var phase = (int)((_submitted - 1) % LsfgShapes.RingFrames);
        var parity = (int)((_submitted - 1) % LsfgGraphBuilder.FrameSlots);

        for (var index = 0; index < made; index++)
        {
            SetTimestamp((index + 1) / (float)(made + 1));
            Run(_graph.Flow[phase]);
            _device.Run(_graph.Generate[parity][index], _sampler);
        }

        return made;
    }

    /// <summary>A generated frame's texture (R8G8B8A8_UNORM), valid until the next <see cref="Generate"/>.</summary>
    public nint Output(int index)
    {
        return _graph.Outputs[index].Texture;
    }

    public void Dispose()
    {
        _arena.Dispose();
        ComObjects.Release(ref _first);
        ComObjects.Release(ref _constants);
        ComObjects.Release(ref _deltaFirst);
        ComObjects.Release(ref _sampler);
    }

    private static ReadOnlySpan<byte> Bytes(in LsfgConstants values)
    {
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in values, 1));
    }

    [HotPath]
    private void SetTimestamp(float timestamp)
    {
        _values.Timestamp = timestamp;
        var shared = _values;
        _device.Update(_constants, Bytes(in shared));
        shared.FirstIter = 1;
        _device.Update(_first, Bytes(in shared));
        shared.FirstIter = 0;

        shared.FirstIterS = 1;
        _device.Update(_deltaFirst, Bytes(in shared));
    }

    [HotPath]
    private void Run(ComputePass[] passes)
    {
        foreach (var pass in passes)
        {
            _device.Run(pass, _sampler);
        }
    }
}
