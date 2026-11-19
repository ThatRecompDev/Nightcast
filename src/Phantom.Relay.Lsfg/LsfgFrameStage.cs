// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;

namespace Phantom.Relay.Lsfg;

/// <summary>
/// The frame generation as the decoder's GPU path runs it, on the decoder's D3D11 device and thread: each new decoded
/// picture (an NV12 surface) is turned into RGBA (<see cref="Nv12ToRgbaConverter"/>) and submitted, the frames between
/// it and the one before are generated (<see cref="Process"/>), and each generated frame is turned back into NV12 into
/// a texture the renderer draws (<see cref="Write"/>). The GPU time of a picture's whole work, from its conversion to
/// the last write (<see cref="Done"/>), is read a few pictures later, never waited for
/// (<see cref="LastGpuMilliseconds"/>). Made from the user's own Lossless.dll, found as
/// <see cref="LosslessLocator"/> finds it; nothing of it is kept once the shaders are made.
/// </summary>
public sealed class LsfgFrameStage : IDisposable
{
    // Timers in flight: a picture's GPU time is read three pictures later, when the GPU is surely past it.
    private const int Timers = 4;

    private readonly LsfgGenerator _generator;
    private readonly Nv12ToRgbaConverter _toRgba;
    private readonly RgbaToNv12Converter _toNv12;
    private readonly GpuTimer[] _timers;
    private readonly bool[] _timing = new bool[Timers];
    private int _timer;

    private LsfgFrameStage(LsfgGenerator generator, Nv12ToRgbaConverter toRgba, RgbaToNv12Converter toNv12)
    {
        _generator = generator;
        _toRgba = toRgba;
        _toNv12 = toNv12;
        _timers = new GpuTimer[Timers];

        for (var index = 0; index < Timers; index++)
        {
            _timers[index] = new GpuTimer(generator.Device);
        }
    }

    public LsfgSettings Settings => _generator.Settings;

    /// <summary>The last new picture as RGBA (the conversion <see cref="Process"/> made), valid until the next picture.</summary>
    public nint Picture => _toRgba.Output;

    /// <summary>The GPU time of the last picture whose time came back, in milliseconds; 0 before any.</summary>
    public double LastGpuMilliseconds { get; private set; }

    /// <summary>
    /// The stage for pictures of <paramref name="width"/> x <paramref name="height"/> on <paramref name="device"/> (the
    /// decoder's ID3D11Device; the stage holds its own references); null and why when Lossless Scaling is not installed,
    /// its shaders do not fit, or the device refuses a step.
    /// </summary>
    public static LsfgFrameStage? TryCreate(nint device, LsfgSettings settings, int width, int height, ILogger log, out string? problem)
    {
        var path = LosslessLocator.Find(settings.LibraryPath, LosslessLocator.SteamLibraries());

        if (path is null)
        {
            problem = "Lossless Scaling is not installed (no Lossless.dll in the Steam libraries or the configured path)";

            return null;
        }

        using var catalog = LsfgShaderCatalog.TryLoad(path, log, out problem);
        var generator = catalog is null ? null : LsfgGenerator.TryCreate(device, catalog, settings, width, height, LsfgFrameFormat.Rgba8, log, out problem);

        if (generator is null)
        {
            return null;
        }

        return Assemble(device, generator, width, height, out problem);
    }

    /// <summary>
    /// Takes a new decoded picture (slice <paramref name="slice"/> of the decoder's <paramref name="surface"/>) and puts
    /// the frames generated between the one before and it in <paramref name="generated"/> (RGBA textures the generator
    /// owns, valid until the next picture); returns how many (none for the first picture, or when a step was refused).
    /// </summary>
    [HotPath]
    public int Process(nint surface, uint slice, Span<nint> generated)
    {
        _timers[_timer].Begin();

        if (!_toRgba.Convert(surface, slice))
        {
            return 0;
        }

        _generator.Submit(_toRgba.Output);

        return _generator.Generate(Settings.Multiplier, generated);
    }

    /// <summary>Writes a generated frame into <paramref name="nv12"/>, an NV12 render target (a slot of the decoder's shared ring).</summary>
    [HotPath]
    public bool Write(nint generated, nint nv12)
    {
        return _toNv12.Convert(generated, nv12);
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
        _generator.Dispose();
    }

    private static LsfgFrameStage? Assemble(nint device, LsfgGenerator generator, int width, int height, out string? problem)
    {
        var toRgba = Nv12ToRgbaConverter.TryCreate(device, width, height, out problem);
        var toNv12 = toRgba is null ? null : RgbaToNv12Converter.TryCreate(device, width, height, out problem);

        if (toRgba is null || toNv12 is null)
        {
            toRgba?.Dispose();
            generator.Dispose();

            return null;
        }

        return new LsfgFrameStage(generator, toRgba, toNv12);
    }
}
