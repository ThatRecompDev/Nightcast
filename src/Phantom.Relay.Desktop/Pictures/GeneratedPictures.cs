// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg;
using Phantom.Relay.Lsfg.Pacing;
using Phantom.Relay.Nvidia;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media.Gpu;
using Phantom.Relay.WebRtc.H264;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// Frame generation on the decoder's GPU path, as the running profile asks for it: Lossless Scaling's
/// (<see cref="LsfgFrameStage"/>) or NVIDIA's FRUC (<see cref="FrucFrameStage"/>), both behind
/// <see cref="IFrameGenerationStage"/>. It is set up for the picture's size on the decoder's device when the settings
/// or the size change (<see cref="Apply"/>), then for each new picture makes the frames between it and the one before
/// (<see cref="Generate"/>), each written into a slot of the shared ring (<see cref="Write"/>). FRUC asked for but
/// missing (no SDK, not an NVIDIA GPU, refused) or failing while it runs is logged once and LSFG runs instead; when
/// anything LSFG needs is missing it logs why once and the stream plays without frame generation.
/// <see cref="Describe"/> says what runs for the stats. Decode thread only, but <see cref="Describe"/>.
/// </summary>
internal sealed class GeneratedPictures : IDisposable
{
    /// <summary>A 30 fps game's new pictures: the pacer's first guess at their interval, which it then measures.</summary>
    private static readonly TimeSpan NominalSourceInterval = TimeSpan.FromSeconds(1.0 / 30);

    /// <summary>
    /// The pacer's first guess at the display's refresh (it shows a slot early by half of one), which it then measures
    /// from its own refreshes: the engine's window says nothing of its display's rate, and the render thread's cadence
    /// is what counts anyway.
    /// </summary>
    private static readonly TimeSpan AssumedRefresh = TimeSpan.FromSeconds(1.0 / 60);

    private const string QualityName = "quality";
    private const string PerformanceName = "performance";
    private const string FrucName = "NVIDIA FRUC";

    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly NvidiaFrucCache _fruc;
    private readonly nint[] _generated = new nint[FrameGeneration.MaximumMultiplier - 1];
    private IFrameGenerationStage? _stage;
    private LsfgPicturePacer? _pacer;
    private FrameGeneration _settings = FrameGeneration.Off;
    private nint _device;
    private uint _lastTimestamp;
    private bool _hasTimestamp;
    private bool _insteadOfFruc;
    private string? _failure;
    private string _state = GpuPictureReport.GenerationOff;
    private double _milliseconds;

    public GeneratedPictures(TimeProvider clock, ILogger log)
    {
        _clock = clock;
        _log = log;
        _fruc = new NvidiaFrucCache(log);
    }

    /// <summary>Whether frames are generated now.</summary>
    public bool IsOn => _stage is not null;

    /// <summary>The picture size the stage was made for.</summary>
    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// Sets the generation up as <paramref name="settings"/> ask, for pictures of <paramref name="width"/> x
    /// <paramref name="height"/> on <paramref name="device"/>, and answers the pacer the pictures are shown by.
    /// </summary>
    public IPicturePacer Apply(FrameGeneration settings, nint device, bool ringWritable, int width, int height)
    {
        _stage?.Dispose();
        _stage = null;
        _pacer = null;
        _hasTimestamp = false;

        _insteadOfFruc = false;
        _failure = null;
        (_settings, _device, Width, Height) = (settings, device, width, height);

        if (!settings.IsOn)
        {
            Volatile.Write(ref _state, GpuPictureReport.GenerationOff);

            return new NewestPicturePacer();
        }

        var problem = ringWritable ? null : "the GPU cannot write the shared NV12 textures with its video processor";

        if (problem is null && settings.Generator == FrameGenerator.NvidiaFruc)
        {
            _stage = Fruc(out problem);
        }

        if (problem is null && _stage is null)
        {
            _stage = Lsfg(out problem);
        }

        return Paced(problem);
    }

    /// <summary>
    /// The frames between the last new picture and this one (slice <paramref name="slice"/> of <paramref name="surface"/>,
    /// its unit's RTP timestamp <paramref name="rtpTimestamp"/>); how many. The pacer spreads them over the real time
    /// between the two pictures, from their timestamps.
    /// </summary>
    [HotPath]
    public int Generate(nint surface, uint slice, uint rtpTimestamp)
    {
        if (_hasTimestamp && _pacer is not null)
        {
            var ticks = unchecked(rtpTimestamp - _lastTimestamp);
            _pacer.NextInterval(TimeSpan.FromSeconds(ticks / (double)H264Format.ClockRate));
        }

        _lastTimestamp = rtpTimestamp;
        _hasTimestamp = true;
        var made = _stage!.Process(surface, slice, _generated);
        _failure = _stage.Failure;

        return made;
    }

    /// <summary>The new picture as RGBA (the stage's own conversion of it), valid until the next picture.</summary>
    public nint Picture => _stage!.Picture;

    /// <summary>Generated frame <paramref name="index"/> (RGBA, the generator's), valid until the next picture.</summary>
    public nint Generated(int index)
    {
        return _generated[index];
    }

    /// <summary>Writes generated frame <paramref name="index"/> into <paramref name="nv12"/>, a slot of the shared ring.</summary>
    [HotPath]
    public bool Write(int index, nint nv12)
    {
        return _stage!.Write(_generated[index], nv12);
    }

    /// <summary>The picture's work is queued: its GPU time is read once it is back; a generator that failed is replaced.</summary>
    [HotPath]
    public void Done()
    {
        _stage!.Done();
        Volatile.Write(ref _milliseconds, _stage.LastGpuMilliseconds);

        if (_failure is not null)
        {
            FallBack(_failure);
        }
    }

    /// <summary>"LSFG x2 (quality, flow 1.00) 0.8 ms" or "NVIDIA FRUC x2 4.3 ms" while one runs, else "frame generation off" and why; any thread.</summary>
    public string Describe()
    {
        var state = Volatile.Read(ref _state);

        if (!IsOn)
        {
            return state;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{state} {Volatile.Read(ref _milliseconds):F1} ms");
    }

    public void Dispose()
    {
        _stage?.Dispose();
        _stage = null;
        _fruc.Dispose();
    }

    // The device's FRUC under a stage of its own; null and why it cannot be (LSFG then runs instead).
    private FrucFrameStage? Fruc(out string? problem)
    {
        var generator = _fruc.TryGet(_device, Width, Height, out var refusal);
        FrucFrameStage? stage = null;

        if (generator is not null)
        {
            stage = FrucFrameStage.TryCreate(generator, _device, _settings.ClampedMultiplier, out refusal);
        }

        if (stage is null)
        {
            Prefer(refusal ?? "it could not be made");
        }

        problem = null;

        return stage;
    }

    private LsfgFrameStage? Lsfg(out string? problem)
    {
        var lsfg = LsfgSettings.Default with
        {
            Enabled = true,
            Multiplier = _settings.ClampedMultiplier,
            FlowScale = _settings.ClampedFlowScale,
            PerformanceMode = _settings.Mode == FrameGenerationMode.Performance,
        };

        return LsfgFrameStage.TryCreate(_device, lsfg, Width, Height, _log, out problem);
    }

    // FRUC failed while it ran: LSFG takes over from the next picture, the pacer staying as it is.
    private void FallBack(string reason)
    {
        _failure = null;
        _stage?.Dispose();
        Prefer($"it stopped: {reason}");
        _stage = Lsfg(out var problem);

        if (_stage is null)
        {
            Off(problem ?? "the frame generation could not be made");
        }
    }

    // FRUC was asked for and LSFG runs instead: logged once, said in the stats.
    private void Prefer(string reason)
    {
        _settings = _settings with { Generator = FrameGenerator.Lsfg };
        _insteadOfFruc = true;
        _log.FrucFallback(reason);
        Volatile.Write(ref _state, LsfgLabel());
    }

    private IPicturePacer Paced(string? problem)
    {
        if (_stage is null)
        {
            Off(problem ?? "the frame generation could not be made");

            return new NewestPicturePacer();
        }

        var multiplier = _settings.ClampedMultiplier;

        if (_settings.Generator == FrameGenerator.NvidiaFruc)
        {
            Volatile.Write(ref _state, string.Create(CultureInfo.InvariantCulture, $"{FrucName} x{multiplier}"));
            _log.FrucOn(multiplier);
        }
        else
        {
            var mode = ModeName();
            Volatile.Write(ref _state, LsfgLabel());
            _log.GenerationOn(multiplier, mode, _settings.ClampedFlowScale);
        }

        _pacer = new LsfgPicturePacer(new FramePacer(_clock, multiplier, NominalSourceInterval, AssumedRefresh));

        return _pacer;
    }

    private void Off(string reason)
    {
        _stage = null;
        Volatile.Write(ref _state, $"{GpuPictureReport.GenerationOff}: {reason}");
        _log.GenerationOff(reason);
    }

    // "LSFG x2 (quality, flow 1.00)", and "instead of NVIDIA FRUC" when FRUC was asked for.
    private string LsfgLabel()
    {
        var label = string.Create(CultureInfo.InvariantCulture, $"LSFG x{_settings.ClampedMultiplier} ({ModeName()}, flow {_settings.ClampedFlowScale:F2})");

        if (_insteadOfFruc)
        {
            label = $"{label} instead of {FrucName}";
        }

        return label;
    }

    private string ModeName()
    {
        if (_settings.Mode == FrameGenerationMode.Performance)
        {
            return PerformanceName;
        }

        return QualityName;
    }
}
