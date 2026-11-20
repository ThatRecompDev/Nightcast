// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Recording;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// shown.mp4: every picture the render thread puts on screen (decoded and generated), at the moment the pacer showed
/// it, encoded on the GPU (<see cref="Mp4VideoEncoder"/>) at the picture's size, before the window's upscale. On the
/// render thread (<see cref="Shown"/>) it only queues the copy of the picture's slot into the encoder's own pool and the
/// moment, from the source's first key frame on; with no free pool texture or a full queue the picture is dropped and
/// counted, never waited for. The first picture shown asks its own thread to make the encoder (a tenth of a second or
/// so; the pictures meanwhile are counted as before the encoder was ready). That thread writes each picture with its
/// time from the first one, held until the next gives its duration. <see cref="Finish"/> stops taking pictures (the
/// render thread is out of <see cref="Shown"/> once it returns), drains, finishes and reads the file back.
/// </summary>
internal sealed class ShownRecorder
{
    /// <summary>The pictures queued at most: a quarter of a second at 120 a second.</summary>
    public const int Capacity = 32;

    private const string NoPicture = "no picture was shown on the GPU path while it recorded";
    private const string NoEncoder = "no encoder";
    private const int BitsPerMegabit = 1_000_000;

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);

    private readonly RecordingQueue<ShownFrame> _queue = new(Capacity);
    private readonly Lock _gate = new();
    private readonly string _path;
    private readonly int _rate;
    private readonly SourceRecorder _source;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly Thread _thread;
    private Mp4VideoEncoder? _encoder;
    private bool _closed;

    // What the first picture asked the encoder for (render thread, once).
    private nint _device;
    private int _width;
    private int _height;
    private int _wanted;

    // Render thread's counts.
    private long _warming;
    private long _busy;
    private long _resized;

    // Writer thread's state.
    private long _firstAt;
    private bool _hasFirst;
    private ShownFrame _held;
    private long _heldTime;
    private bool _holding;
    private long _written;
    private long _failed;
    private string? _problem;
    private string _encoderName = NoEncoder;

    /// <summary>Writes <paramref name="path"/>, its encoder budgeted for <paramref name="rate"/> pictures a second, from <paramref name="source"/>'s start on.</summary>
    public ShownRecorder(string path, int rate, SourceRecorder source, TimeProvider clock, ILogger log)
    {
        _path = path;
        _rate = rate;
        _source = source;
        _clock = clock;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "Relay recording shown" };
    }

    /// <summary>Whether the encoder is made, so the pictures shown are recorded.</summary>
    public bool IsReady => Volatile.Read(ref _encoder) is not null;

    public void Start()
    {
        _thread.Start();
    }

    /// <summary>
    /// The render thread put a picture on screen: <paramref name="texture"/> (NV12, its top-left <paramref name="width"/> x
    /// <paramref name="height"/> the picture) on <paramref name="device"/>. Its copy is queued; nothing waits.
    /// </summary>
    [HotPath]
    public void Shown(nint device, nint texture, int width, int height)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            var encoder = Volatile.Read(ref _encoder);

            if (encoder is null)
            {
                Want(device, width, height);
            }
            else if (_source.HasStarted)
            {
                Record(encoder, texture, width, height);
            }
        }
    }

    /// <summary>No more pictures: what is queued is written, the file finished and read back; how it came out.</summary>
    public ShownOutcome Finish()
    {
        lock (_gate)
        {
            _closed = true;
        }

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

        return new ShownOutcome(facts, problem, _encoderName, _written, Offset(), _warming, _busy, _queue.Dropped, _resized, _failed);
    }

    [HotPath]
    private void Record(Mp4VideoEncoder encoder, nint texture, int width, int height)
    {
        if (width != _width || height != _height)
        {
            _resized++;

            return;
        }

        if (!encoder.TryCopy(texture, 0, out var slot))
        {
            _busy++;

            return;
        }

        if (!_queue.TryAdd(new ShownFrame(slot, _clock.GetTimestamp())))
        {
            encoder.Abandon(slot);
        }
    }

    // The first picture names the device and the size; the writer makes the encoder for them.
    [HotPath]
    private void Want(nint device, int width, int height)
    {
        if (_source.HasStarted)
        {
            _warming++;
        }

        if (_wanted != 0)
        {
            return;
        }

        (_device, _width, _height) = (device, width, height);
        Volatile.Write(ref _wanted, 1);
    }

    private void Run()
    {
        try
        {
            WriteAll();
        }
        catch (Exception error)
        {
            _problem = error.Message;
            _log.ShownSkipped(error.Message);
        }
    }

    private void WriteAll()
    {
        while (true)
        {
            Prepare();

            if (_queue.TryTake(out var frame, Poll))
            {
                Write(frame);

                continue;
            }

            if (_queue.IsCompleted)
            {
                return;
            }
        }
    }

    // The encoder, once the first picture asked for it: H.264 up to NVENC's 4096, HEVC beyond.
    private void Prepare()
    {
        if (Volatile.Read(ref _encoder) is not null || _problem is not null || Volatile.Read(ref _wanted) == 0)
        {
            return;
        }

        var codec = RecordedVideoCodec.H264;

        if (Math.Max(_width, _height) > CompareArguments.MaxH264Width)
        {
            codec = RecordedVideoCodec.Hevc;
        }

        var settings = new VideoEncoderSettings(_width, _height, _rate, VideoEncoderSettings.HighBitrate, codec);
        var encoder = Mp4VideoEncoder.TryCreate(_device, _path, settings, out var problem);

        if (encoder is null)
        {
            _problem = problem;
            _log.ShownSkipped(problem ?? "the encoder could not be made");

            return;
        }

        Named(encoder);
        Volatile.Write(ref _encoder, encoder);
    }

    private void Named(Mp4VideoEncoder encoder)
    {
        var hardware = encoder.IsHardware ? "on the GPU" : "in software";
        _encoderName = $"{encoder.EncoderName} ({hardware}, {encoder.Codec} at {VideoEncoderSettings.HighBitrate / BitsPerMegabit} Mbit/s)";

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.ShownEncoder(encoder.EncoderName, hardware, encoder.Codec.ToString(), encoder.Width, encoder.Height, _rate);
        }
    }

    // Each picture's time from the first; each waits for the next to give its duration.
    private void Write(in ShownFrame frame)
    {
        if (!_hasFirst)
        {
            (_firstAt, _hasFirst) = (frame.ShownAt, true);
        }

        var time = Units(frame.ShownAt - _firstAt);

        if (_holding)
        {
            WriteHeld(time - _heldTime);
        }

        (_held, _heldTime, _holding) = (frame, time, true);
    }

    private void WriteHeld(long duration)
    {
        if (_encoder!.TryWrite(_held.Slot, _heldTime, Math.Max(1, duration)))
        {
            _written++;
        }
        else
        {
            _failed++;
        }
    }

    // The last picture with a frame's nominal duration, then the encoder drained and the file finished.
    private string? Close()
    {
        var encoder = Volatile.Read(ref _encoder);

        if (encoder is null)
        {
            return _problem ?? NoPicture;
        }

        if (_holding)
        {
            WriteHeld(RtpTimeline.UnitsPerSecond / _rate);
        }

        _ = encoder.TryFinish(out var problem);
        encoder.Dispose();
        Volatile.Write(ref _encoder, null);

        return problem ?? _problem;
    }

    // How much later than the source's first key frame the first picture recorded here was shown.
    private TimeSpan Offset()
    {
        if (!_hasFirst || !_source.HasStarted)
        {
            return TimeSpan.Zero;
        }

        return _clock.GetElapsedTime(_source.Origin, _firstAt);
    }

    private long Units(long ticks)
    {
        return (long)(ticks * (double)RtpTimeline.UnitsPerSecond / _clock.TimestampFrequency);
    }
}
