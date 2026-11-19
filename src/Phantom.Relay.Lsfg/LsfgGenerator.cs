// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg.Logging;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg;

/// <summary>
/// Lossless Scaling's frame generation on a D3D11 device (the hardware decoder's), from the shaders of the user's
/// own Lossless.dll. Real frames go in with <see cref="Submit"/>; <see cref="Generate(int, Span{nint})"/> then makes
/// the frames between the last two. Textures travel as raw ID3D11Texture2D pointers: frames in are R8G8B8A8 or
/// B8G8R8A8 of the size given at creation (with shader-resource binding); generated frames out are R8G8B8A8 textures
/// the generator owns, valid until the next call.
/// </summary>
public sealed class LsfgGenerator : IDisposable
{
    private readonly ComputeDevice _device;
    private readonly LsfgShaderSet _shaders;
    private readonly LsfgPipeline _pipeline;

    private LsfgGenerator(ComputeDevice device, LsfgShaderSet shaders, LsfgPipeline pipeline, LsfgSettings settings, int width, int height)
    {
        _device = device;
        _shaders = shaders;
        _pipeline = pipeline;
        Settings = settings;
        Width = width;
        Height = height;
    }

    public LsfgSettings Settings { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The flow's size: the frame's size times the flow scale.</summary>
    public int FlowWidth => _pipeline.FlowWidth;

    public int FlowHeight => _pipeline.FlowHeight;

    /// <summary>The real frames submitted so far.</summary>
    public long SubmittedFrames => _pipeline.Submitted;

    internal ComputeDevice Device => _device;

    /// <summary>
    /// The generator for frames of the given size and format on <paramref name="device"/> (an ID3D11Device pointer the
    /// caller keeps owning; the generator holds its own reference); null and why when the shaders or the device
    /// refuse. The catalog must stay open while the generator is created; it may be disposed after.
    /// </summary>
    public static LsfgGenerator? TryCreate(nint device, LsfgShaderCatalog catalog, LsfgSettings settings, int width, int height, LsfgFrameFormat format, ILogger log, out string? problem)
    {
        var normalized = settings.Normalized();
        var compute = ComputeDevice.Open(new StrategyBasedComWrappers(), device);
        var shaders = LsfgShaderSet.TryCreate(compute, catalog, normalized.PerformanceMode, out problem);

        if (shaders is null)
        {
            compute.Dispose();
            log.Unavailable(problem ?? "the shaders could not be created");

            return null;
        }

        var pixelFormat = format == LsfgFrameFormat.Bgra8 ? Direct3D11Values.Bgra8Format : Direct3D11Values.Rgba8Format;
        var pipeline = LsfgPipeline.TryBuild(compute, shaders, normalized, width, height, pixelFormat, out problem);

        if (pipeline is null)
        {
            shaders.Dispose();
            compute.Dispose();
            log.Unavailable(problem ?? "the pipeline could not be built");

            return null;
        }

        var mode = normalized.PerformanceMode ? "performance" : "quality";
        log.PipelineReady(width, height, pipeline.FlowWidth, pipeline.FlowHeight, mode, pipeline.PassesPerFrame, pipeline.PassesPerGenerated);

        return new LsfgGenerator(compute, shaders, pipeline, normalized, width, height);
    }

    /// <summary>Takes a real frame: copies it on the GPU (the caller may reuse the texture) and runs its passes.</summary>
    [HotPath]
    public void Submit(nint frame)
    {
        _pipeline.Submit(frame);
    }

    /// <summary>
    /// Makes <paramref name="multiplier"/> - 1 frames between the last two submitted frames, in display order, and puts
    /// their textures in <paramref name="outputs"/>; returns how many (none before two real frames).
    /// </summary>
    [HotPath]
    public int Generate(int multiplier, Span<nint> outputs)
    {
        var wanted = Math.Min(Math.Clamp(multiplier, LsfgSettings.MinimumMultiplier, LsfgSettings.MaximumMultiplier) - 1, outputs.Length);
        var made = _pipeline.Generate(wanted);

        for (var index = 0; index < made; index++)
        {
            outputs[index] = _pipeline.Output(index);
        }

        return made;
    }

    /// <summary>Submits <paramref name="previous"/> and <paramref name="current"/>, then generates the frames between them.</summary>
    public int Generate(nint previous, nint current, int multiplier, Span<nint> outputs)
    {
        Submit(previous);
        Submit(current);

        return Generate(multiplier, outputs);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _shaders.Dispose();
        _device.Dispose();
    }
}
