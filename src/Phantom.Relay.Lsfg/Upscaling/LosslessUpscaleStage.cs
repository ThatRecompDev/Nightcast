// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// A Lossless Scaling upscaler as the decoder's GPU path runs it, on the decoder's D3D11 device and thread: a decoded
/// picture (an NV12 surface, <see cref="Write(nint, uint, nint)"/>) or an RGBA one (a generated frame, or the frame
/// generation's own conversion of the decoded picture, <see cref="WriteRgba"/>) is upscaled and turned back into NV12
/// into a texture the renderer draws (a slot of the decoder's shared ring, of the geometry's output texture size). The
/// GPU time of a picture's whole work, between <see cref="Begin"/> and <see cref="Done"/>, is read a few pictures later,
/// never waited for (<see cref="LastGpuMilliseconds"/>). Made from the user's own Lossless.dll, found as
/// <see cref="LosslessLocator"/> finds it; nothing of it is kept once the shaders are made.
/// </summary>
public sealed class LosslessUpscaleStage : IDisposable
{
    // Timers in flight: a picture's GPU time is read three pictures later, when the GPU is surely past it.
    private const int Timers = 4;

    private readonly LosslessUpscaler _upscaler;
    private readonly Nv12ToRgbaConverter _toRgba;
    private readonly RgbaToNv12Converter _toNv12;
    private readonly GpuTimer[] _timers = new GpuTimer[Timers];
    private readonly bool[] _timing = new bool[Timers];
    private int _timer;

    private LosslessUpscaleStage(LosslessUpscaler upscaler, Nv12ToRgbaConverter toRgba, RgbaToNv12Converter toNv12)
    {
        _upscaler = upscaler;
        _toRgba = toRgba;
        _toNv12 = toNv12;

        for (var index = 0; index < Timers; index++)
        {
            _timers[index] = new GpuTimer(upscaler.Device);
        }
    }

    public LosslessUpscalerSettings Settings => _upscaler.Settings;

    public LosslessUpscaleGeometry Geometry => _upscaler.Geometry;

    /// <summary>The GPU time of the last picture whose time came back, in milliseconds; 0 before any.</summary>
    public double LastGpuMilliseconds { get; private set; }

    /// <summary>
    /// The stage for <paramref name="settings"/> at <paramref name="geometry"/> on <paramref name="device"/> (the
    /// decoder's ID3D11Device; the stage holds its own references); null and why when Lossless Scaling is not
    /// installed, its shaders do not fit, or the device refuses a step.
    /// </summary>
    public static LosslessUpscaleStage? TryCreate(nint device, LosslessUpscalerSettings settings, LosslessUpscaleGeometry geometry, ILogger log, out string? problem)
    {
        var path = LosslessLocator.Find(settings.LibraryPath, LosslessLocator.SteamLibraries());

        if (path is null)
        {
            problem = "Lossless Scaling is not installed (no Lossless.dll in the Steam libraries or the configured path)";

            return null;
        }

        using var catalog = LsfgShaderCatalog.TryLoad(path, log, out problem);
        var upscaler = catalog is null ? null : LosslessUpscaler.TryCreate(device, catalog, settings, geometry, log, out problem);

        if (upscaler is null)
        {
            return null;
        }

        return Assemble(device, upscaler, out problem);
    }

    /// <summary>The picture's work starts: its GPU time is taken from here.</summary>
    [HotPath]
    public void Begin()
    {
        _timers[_timer].Begin();
    }

    /// <summary>Upscales slice <paramref name="slice"/> of the decoder's <paramref name="surface"/> into <paramref name="nv12"/>; false when a step was refused.</summary>
    [HotPath]
    public bool Write(nint surface, uint slice, nint nv12)
    {
        return _toRgba.Convert(surface, slice) && WriteRgba(_toRgba.Output, nv12);
    }

    /// <summary>Upscales <paramref name="rgba"/> (an R8G8B8A8 texture of the input size) into <paramref name="nv12"/>; false when a step was refused.</summary>
    [HotPath]
    public bool WriteRgba(nint rgba, nint nv12)
    {
        return _upscaler.Upscale(rgba) && _toNv12.Convert(_upscaler.Output, nv12);
    }

    /// <summary>The picture's work is all queued: its timing ends, and the oldest timing still out is read if it is back.</summary>
    [HotPath]
    public void Done()
    {
        _timers[_timer].End();
        _timing[_timer] = true;
        _timer = (_timer + 1) % Timers;

        if (_timing[_timer] && _timers[_timer].TryRead(out var milliseconds))
        {
            LastGpuMilliseconds = milliseconds;
            _timing[_timer] = false;
        }
    }

    public void Dispose()
    {
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }

        _toNv12.Dispose();
        _toRgba.Dispose();
        _upscaler.Dispose();
    }

    private static LosslessUpscaleStage? Assemble(nint device, LosslessUpscaler upscaler, out string? problem)
    {
        var geometry = upscaler.Geometry;
        var toRgba = Nv12ToRgbaConverter.TryCreate(device, geometry.InputWidth, geometry.InputHeight, out problem);
        var toNv12 = toRgba is null ? null : RgbaToNv12Converter.TryCreate(device, geometry.OutputTextureWidth, geometry.OutputTextureHeight, out problem);

        if (toRgba is null || toNv12 is null)
        {
            toRgba?.Dispose();
            upscaler.Dispose();

            return null;
        }

        return new LosslessUpscaleStage(upscaler, toRgba, toNv12);
    }
}
