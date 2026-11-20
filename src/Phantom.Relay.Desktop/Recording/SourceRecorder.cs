// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Media.Windows.Recording;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// source.mp4: the console's access units as the decode thread takes them (repeats included), remuxed without
/// re-encoding. On the decode thread (<see cref="Received"/>) it only queues the worker's own copy of each unit, from the
/// first key frame on (the units before it are skipped and counted; the moment of that key frame is the recording's
/// <see cref="Origin"/>); a full queue drops and counts, never waits. Its own thread writes the units with their times
/// from the RTP clock (<see cref="RtpTimeline"/>), each unit held until the next gives its duration, and asks the
/// console for a key frame again every second until one comes. <see cref="Finish"/> drains, finishes and reads the
/// file back.
/// </summary>
internal sealed class SourceRecorder : IAccessUnitListener
{
    /// <summary>The units queued at most: about four seconds of the console's 60 a second.</summary>
    public const int Capacity = 256;

    private const string NoKeyFrame = "no key frame came while it recorded";

    private static readonly TimeSpan KeyFrameRetry = TimeSpan.FromSeconds(1);

    private readonly RecordingQueue<SourceUnit> _queue = new(Capacity);
    private readonly RtpTimeline _timeline = new();
    private readonly string _path;
    private readonly int _width;
    private readonly int _height;
    private readonly AccessUnitTap _tap;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly Thread _thread;
    private Mp4H264Remuxer? _remuxer;
    private SourceUnit _held;
    private long _heldTime;
    private bool _holding;
    private long _origin;
    private int _started;
    private long _skipped;
    private long _written;
    private long _failed;
    private string? _problem;

    /// <summary>Writes <paramref name="path"/> for a stream of <paramref name="width"/> x <paramref name="height"/>; <paramref name="tap"/> asks for key frames.</summary>
    public SourceRecorder(string path, int width, int height, AccessUnitTap tap, TimeProvider clock, ILogger log)
    {
        _path = path;
        _width = width;
        _height = height;
        _tap = tap;

        _clock = clock;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "Relay recording source" };
    }

    /// <summary>Whether the first key frame came.</summary>
    public bool HasStarted => Volatile.Read(ref _started) != 0;

    /// <summary>When the first key frame came (the clock's timestamp); valid once <see cref="HasStarted"/>.</summary>
    public long Origin => Interlocked.Read(ref _origin);

    public void Start()
    {
        _thread.Start();
    }

    /// <summary>The decode thread took a unit: queued from the first key frame on, never waited for.</summary>
    public void Received(byte[] unit, uint rtpTimestamp, bool key)
    {
        if (!HasStarted)
        {
            if (!key)
            {
                Interlocked.Increment(ref _skipped);

                return;
            }

            Interlocked.Exchange(ref _origin, _clock.GetTimestamp());
            Volatile.Write(ref _started, 1);
        }

        _ = _queue.TryAdd(new SourceUnit(unit, rtpTimestamp, key));
    }

    /// <summary>No more units: what is queued is written, the file finished and read back; how it came out.</summary>
    public SourceOutcome Finish()
    {
        _queue.Complete();

        if (_thread.IsAlive)
        {
            _thread.Join();
        }

        var problem = Close();
        Mp4Facts? facts = null;

        if (problem is null && _written > 0)
        {
            facts = Mp4Reader.TryRead(_path, out problem);
        }

        return new SourceOutcome(facts, problem, _written, Interlocked.Read(ref _skipped), _queue.Dropped, _failed);
    }

    // The writer: each unit as it comes; while none has started, a key frame asked for again every second.
    private void Run()
    {
        try
        {
            WriteAll();
        }
        catch (Exception error)
        {
            _problem = error.Message;
            _log.SourceRefused(error.Message);
        }
    }

    private void WriteAll()
    {
        while (true)
        {
            if (_queue.TryTake(out var unit, KeyFrameRetry))
            {
                Write(unit);

                continue;
            }

            if (_queue.IsCompleted)
            {
                return;
            }

            if (!HasStarted)
            {
                _tap.RequestKeyframe();
            }
        }
    }

    // The file opens at the first unit (a key frame, its parameter sets describe the stream); each unit waits for the next.
    private void Write(in SourceUnit unit)
    {
        if (_remuxer is null && _problem is null)
        {
            Open(unit);
        }

        if (_remuxer is null)
        {
            _failed++;

            return;
        }

        var time = _timeline.Next(unit.RtpTimestamp);

        if (_holding)
        {
            WriteHeld(time - _heldTime);
        }

        (_held, _heldTime, _holding) = (unit, time, true);
    }

    private void Open(in SourceUnit unit)
    {
        _remuxer = Mp4H264Remuxer.TryCreate(_path, _width, _height, CompareLabels.ConsoleRate, unit.Unit, out var problem);

        if (_remuxer is null)
        {
            _problem = problem;
            _log.SourceRefused(problem ?? "unknown");
        }
    }

    private void WriteHeld(long duration)
    {
        if (_remuxer!.TryWrite(_held.Unit, _heldTime, Math.Max(1, duration), _held.Key))
        {
            _written++;
        }
        else
        {
            _failed++;
        }
    }

    // The last unit with a unit's nominal duration, then the file's index; why it could not be finished, or null.
    private string? Close()
    {
        if (_remuxer is null)
        {
            return _problem ?? NoKeyFrame;
        }

        if (_holding)
        {
            WriteHeld(RtpTimeline.UnitsPerSecond / CompareLabels.ConsoleRate);
        }

        _ = _remuxer.TryFinish(out var problem);
        _remuxer.Dispose();
        _remuxer = null;

        return problem ?? _problem;
    }
}
