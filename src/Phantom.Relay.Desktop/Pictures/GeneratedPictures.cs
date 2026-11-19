// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg;
using Phantom.Relay.Lsfg.Pacing;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media.Gpu;
using Phantom.Relay.WebRtc.H264;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// Lossless Scaling's frame generation on the decoder's GPU path, as the running profile asks for it: set up for the
/// picture's size on the decoder's device when the settings or the size change (<see cref="Apply"/>), then for each
/// new picture the frames between it and the one before (<see cref="Generate"/>), each written into a slot of the
/// shared ring (<see cref="Write"/>). When anything it needs is missing (Lossless Scaling not installed, shaders that do
/// not fit, a ring the video processor cannot write) it logs why once and the stream plays without it;
/// <see cref="Describe"/> says what it does for the stats. Decode thread only, but <see cref="Describe"/>.
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

    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly nint[] _generated = new nint[FrameGeneration.MaximumMultiplier - 1];
    private LsfgFrameStage? _stage;
    private LsfgPicturePacer? _pacer;
    private uint _lastTimestamp;
    private bool _hasTimestamp;
    private string _state = GpuPictureReport.GenerationOff;
    private double _milliseconds;

    public GeneratedPictures(TimeProvider clock, ILogger log)
    {
        _clock = clock;
        _log = log;
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
        Width = width;
        Height = height;

        if (!settings.IsOn)
        {
            Volatile.Write(ref _state, GpuPictureReport.GenerationOff);

            return new NewestPicturePacer();
        }

        var problem = ringWritable ? null : "the GPU cannot write the shared NV12 textures with its video processor";
        var lsfg = LsfgSettings.Default with
        {
            Enabled = true,
            Multiplier = settings.ClampedMultiplier,
            FlowScale = settings.ClampedFlowScale,
            PerformanceMode = settings.Mode == FrameGenerationMode.Performance,
        };

        if (problem is null)
        {
            _stage = LsfgFrameStage.TryCreate(device, lsfg, width, height, _log, out problem);
        }

        return Paced(settings, problem);
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

        return _stage!.Process(surface, slice, _generated);
    }

    /// <summary>Writes generated frame <paramref name="index"/> into <paramref name="nv12"/>, a slot of the shared ring.</summary>
    [HotPath]
    public bool Write(int index, nint nv12)
    {
        return _stage!.Write(_generated[index], nv12);
    }

    /// <summary>The picture's work is queued: its GPU time is read once it is back.</summary>
    [HotPath]
    public void Done()
    {
        _stage!.Done();
        Volatile.Write(ref _milliseconds, _stage.LastGpuMilliseconds);
    }

    /// <summary>"LSFG x2 (quality, flow 1.00) 0.5 ms" while it runs, else "LSFG off" and why; any thread.</summary>
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
    }

    private IPicturePacer Paced(FrameGeneration settings, string? problem)
    {
        if (_stage is null)
        {
            var reason = problem ?? "the frame generation could not be made";
            Volatile.Write(ref _state, $"{GpuPictureReport.GenerationOff}: {reason}");
            _log.GenerationOff(reason);

            return new NewestPicturePacer();
        }

        var multiplier = settings.ClampedMultiplier;
        var mode = settings.Mode == FrameGenerationMode.Performance ? PerformanceName : QualityName;
        Volatile.Write(ref _state, string.Create(CultureInfo.InvariantCulture, $"LSFG x{multiplier} ({mode}, flow {settings.ClampedFlowScale:F2})"));
        _log.GenerationOn(multiplier, mode, settings.ClampedFlowScale);

        _pacer = new LsfgPicturePacer(new FramePacer(_clock, multiplier, NominalSourceInterval, AssumedRefresh));

        return _pacer;
    }
}
