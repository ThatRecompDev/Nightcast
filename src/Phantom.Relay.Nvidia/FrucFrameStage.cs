// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Lsfg;
using Phantom.Relay.Lsfg.Pipeline;

namespace Phantom.Relay.Nvidia;

/// <summary>
/// NVIDIA FRUC as the decoder's GPU path runs it (<see cref="IFrameGenerationStage"/>), the way LSFG's stage runs: each
/// new decoded picture turned into RGBA on the video processor (<see cref="Nv12ToRgbaConverter"/>; FRUC's NV12 path
/// leaves the chroma plane unwritten), submitted to the generator, and each generated frame turned back into NV12 into
/// a slot of the shared ring (<see cref="RgbaToNv12Converter"/>). The generator is borrowed: FRUC can be made once per
/// device, so its owner keeps it while the device lives and this stage only pauses it when it goes
/// (<see cref="FrucGenerator.Restart"/>). FRUC's call holds the decode thread until its work is done (about 4 ms a
/// frame at 1440p); the GPU time from the conversion to the last write is read a few pictures later.
/// </summary>
public sealed class FrucFrameStage : IFrameGenerationStage
{
    // Timers in flight: a picture's GPU time is read three pictures later, when the GPU is surely past it.
    private const int Timers = 4;

    private readonly FrucGenerator _generator;
    private readonly Nv12ToRgbaConverter _toRgba;
    private readonly RgbaToNv12Converter _toNv12;
    private readonly int _multiplier;
    private readonly GpuTimer[] _timers = new GpuTimer[Timers];
    private readonly bool[] _timing = new bool[Timers];
    private int _timer;

    private FrucFrameStage(FrucGenerator generator, Nv12ToRgbaConverter toRgba, RgbaToNv12Converter toNv12, int multiplier)
    {
        _generator = generator;
        _toRgba = toRgba;
        _toNv12 = toNv12;
        _multiplier = multiplier;

        for (var index = 0; index < Timers; index++)
        {
            _timers[index] = new GpuTimer(generator.Device);
        }

        generator.Restart();
    }

    public nint Picture => _toRgba.Output;

    public double LastGpuMilliseconds { get; private set; }

    public string? Failure => _generator.Failure;

    /// <summary>
    /// The stage over <paramref name="generator"/> (RGBA, made on the decoder's <paramref name="device"/> for its
    /// pictures' size) giving <paramref name="multiplier"/> - 1 frames per new picture (x2 to x4); null and why when the
    /// video processor refuses a conversion.
    /// </summary>
    public static FrucFrameStage? TryCreate(FrucGenerator generator, nint device, int multiplier, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(generator);
        var toRgba = Nv12ToRgbaConverter.TryCreate(device, generator.Width, generator.Height, out problem);
        var toNv12 = toRgba is null ? null : RgbaToNv12Converter.TryCreate(device, generator.Width, generator.Height, out problem);

        if (toRgba is null || toNv12 is null)
        {
            toRgba?.Dispose();

            return null;
        }

        return new FrucFrameStage(generator, toRgba, toNv12, multiplier);
    }

    [HotPath]
    public int Process(nint surface, uint slice, Span<nint> generated)
    {
        _timers[_timer].Begin();

        if (_generator.Failure is not null || !_toRgba.Convert(surface, slice))
        {
            return 0;
        }

        var made = Math.Min(_generator.Submit(_toRgba.Output, 0, _multiplier), generated.Length);

        for (var index = 0; index < made; index++)
        {
            generated[index] = _generator.Output(index);
        }

        return made;
    }

    [HotPath]
    public bool Write(nint generated, nint nv12)
    {
        return _toNv12.Convert(generated, nv12);
    }

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

    /// <summary>The stage goes; the generator stays its owner's, paused.</summary>
    public void Dispose()
    {
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }

        _toNv12.Dispose();
        _toRgba.Dispose();
        _generator.Restart();
    }
}
