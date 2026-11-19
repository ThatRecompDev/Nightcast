// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg.Logging;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// One of Lossless Scaling's upscalers (LS1, its performance setting, SGSR) on a D3D11 device (the hardware decoder's),
/// from the shaders of the user's own Lossless.dll: an R8G8B8A8 picture of the geometry's input size in, the upscaled
/// picture out (<see cref="Output"/>, R8G8B8A8, owned by the upscaler, valid until the next call). Everything is made
/// once for its sizes; <see cref="Upscale"/> only binds and dispatches. The shader views of the pictures it is given are
/// made once per texture and kept (a handful: the decoder's converter and the frame generation's outputs).
/// </summary>
public sealed class LosslessUpscaler : IDisposable
{
    // The textures a stream hands in are few (one converted picture, up to three generated ones); views beyond are made per call.
    private const int MaxCachedViews = 8;

    private readonly ComputeDevice _device;
    private readonly LosslessUpscaleShaders _shaders;
    private readonly LosslessUpscaleGraph _graph;
    private readonly (nint Texture, nint View)[] _views = new (nint, nint)[MaxCachedViews];
    private nint _sampler;
    private int _cached;

    private LosslessUpscaler(ComputeDevice device, LosslessUpscaleShaders shaders, LosslessUpscaleGraph graph, nint sampler, LosslessUpscalerSettings settings, LosslessUpscaleGeometry geometry)
    {
        _device = device;
        _shaders = shaders;
        _graph = graph;
        _sampler = sampler;
        Settings = settings;
        Geometry = geometry;
    }

    public LosslessUpscalerSettings Settings { get; }

    public LosslessUpscaleGeometry Geometry { get; }

    /// <summary>The upscaled picture's texture (R8G8B8A8, the geometry's output texture size).</summary>
    public nint Output => _graph.Output.Texture;

    /// <summary>The dispatches one picture costs.</summary>
    public int Passes => _graph.Passes.Length;

    internal ComputeDevice Device => _device;

    /// <summary>
    /// The upscaler for <paramref name="settings"/> at <paramref name="geometry"/> on <paramref name="device"/> (an
    /// ID3D11Device the caller keeps owning; the upscaler holds its own reference); null and why when the shaders or the
    /// device refuse. The catalog must stay open while it is created; it may be disposed after.
    /// </summary>
    public static LosslessUpscaler? TryCreate(nint device, LsfgShaderCatalog catalog, LosslessUpscalerSettings settings, LosslessUpscaleGeometry geometry, ILogger log, out string? problem)
    {
        return TryCreate(device, catalog, settings, geometry, LosslessUpscaleWiring.Standard, log, out problem);
    }

    /// <summary>As the public one, with the shaders' open choices made as <paramref name="wiring"/> says (for measurements).</summary>
    internal static LosslessUpscaler? TryCreate(nint device, LsfgShaderCatalog catalog, LosslessUpscalerSettings settings, LosslessUpscaleGeometry geometry, LosslessUpscaleWiring wiring, ILogger log, out string? problem)
    {
        var normalized = settings.Normalized();

        if (!geometry.IsValid)
        {
            problem = $"the upscaler cannot scale {geometry.InputWidth}x{geometry.InputHeight} to {geometry.OutputWidth}x{geometry.OutputHeight}";
            log.UpscalerUnavailable(problem);

            return null;
        }

        var compute = ComputeDevice.Open(new StrategyBasedComWrappers(), device);
        var upscaler = Assemble(compute, catalog, normalized, geometry, wiring, out problem);

        if (upscaler is null)
        {
            compute.Dispose();
            log.UpscalerUnavailable(problem ?? "the upscaler could not be built");

            return null;
        }

        log.UpscalerReady(normalized.Kind, normalized.Level, geometry.InputWidth, geometry.InputHeight, geometry.OutputWidth, geometry.OutputHeight, upscaler.Passes);

        return upscaler;
    }

    /// <summary>Upscales <paramref name="picture"/> (an R8G8B8A8 texture of the input size) into <see cref="Output"/>; false when D3D11 refuses its view.</summary>
    [HotPath]
    public bool Upscale(nint picture)
    {
        var view = ViewOf(picture, out var owned);

        if (view == 0)
        {
            return false;
        }

        foreach (var (pass, slot) in _graph.PictureSlots)
        {
            _graph.Passes[pass].Inputs[slot] = view;
        }

        foreach (var pass in _graph.Passes)
        {
            _device.Run(pass, _sampler);
        }

        if (owned)
        {
            ComObjects.Release(ref view);
        }

        return true;
    }

    public void Dispose()
    {
        for (var index = 0; index < _cached; index++)
        {
            ComObjects.Release(ref _views[index].View);
        }

        _cached = 0;
        ComObjects.Release(ref _sampler);
        _graph.Dispose();
        _shaders.Dispose();
        _device.Dispose();
    }

    private static LosslessUpscaler? Assemble(ComputeDevice compute, LsfgShaderCatalog catalog, LosslessUpscalerSettings settings, LosslessUpscaleGeometry geometry, LosslessUpscaleWiring wiring, out string? problem)
    {
        var shaders = LosslessUpscaleShaders.TryCreate(compute, catalog, LosslessUpscaleGraph.Roles(settings), out problem);
        var graph = shaders is null ? null : LosslessUpscaleGraph.TryBuild(compute, shaders, settings, geometry, wiring, out problem);
        var sampler = graph is null ? 0 : compute.CreateLinearClampSampler();

        if (shaders is null || graph is null || sampler == 0)
        {
            graph?.Dispose();
            shaders?.Dispose();
            problem ??= "D3D11 refused the upscaler's sampler";

            return null;
        }

        return new LosslessUpscaler(compute, shaders, graph, sampler, settings, geometry);
    }

    // The picture's shader view: kept per texture (a view holds its texture, so a kept view's texture cannot be another's).
    [HotPath]
    private nint ViewOf(nint texture, out bool owned)
    {
        owned = false;

        for (var index = 0; index < _cached; index++)
        {
            if (_views[index].Texture == texture)
            {
                return _views[index].View;
            }
        }

        var view = _device.CreateView(texture, Direct3D11Values.Rgba8Format);

        if (view == 0 || _cached == _views.Length)
        {
            owned = view != 0;

            return view;
        }

        _views[_cached] = (texture, view);
        _cached++;

        return view;
    }
}
