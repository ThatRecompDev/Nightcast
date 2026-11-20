// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Concurrent;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Decodes access units on a thread of its own, so the network loop never waits on the decoder: units are queued as
/// they complete, and the worker times each decode. The decoder is the head's choice (openh264 in software today, a
/// hardware decoder later) and stays the head's to dispose. Each picture goes straight into the
/// <see cref="PictureMailbox"/>, read back to the CPU; a decoder that is also an <see cref="IGpuPictureProducer"/>
/// hands its pictures to the renderer on the GPU instead while it shares. The worker times the hand-off, the CPU's
/// part of getting a decoded picture to the renderer (the read-back, or the GPU copy's queueing). Each unit is offered
/// to the <see cref="Tap"/> before it is decoded (a recorder's listener queues it). The counters are safe to read from
/// any thread.
/// </summary>
public sealed class DecodeWorker : IDisposable
{
    private readonly BlockingCollection<(byte[] Unit, uint Timestamp, bool Key)> _queue = [];
    private readonly IVideoDecoder _decoder;
    private readonly IGpuPictureProducer? _gpu;
    private readonly PictureMailbox _mailbox;
    private readonly AccessUnitTap _tap;
    private readonly TimeProvider _clock;
    private readonly RepeatClassifier _repeats = new(RepeatSettings.Standard);
    private readonly Thread _thread;
    private readonly int[] _sizes = new int[SizeHistory];
    private int _sizesWritten;

    private long _pictures;
    private long _decoded;
    private long _news;
    private long _repeated;
    private int _cadence;
    private long _decodeTicks;
    private long _handOffTicks;
    private long _worstTicks;
    private long _windowWorstTicks;
    private int _deepestQueue;
    private int _width;
    private int _height;
    private bool _started;
    private string? _problem;

    /// <summary>The <paramref name="tap"/> sees every unit on this worker's thread and carries key-frame requests to the network thread.</summary>
    public DecodeWorker(IVideoDecoder decoder, PictureMailbox mailbox, AccessUnitTap tap, TimeProvider clock)
    {
        _decoder = decoder;
        _gpu = decoder as IGpuPictureProducer;
        _mailbox = mailbox;
        _tap = tap;
        _clock = clock;
        _thread = new Thread(Run) { IsBackground = true, Name = "Relay video decode" };
    }

    /// <summary>How many of the latest access units' sizes are kept for the log (<see cref="RecentSizes"/>).</summary>
    public const int SizeHistory = 12;

    /// <summary>Pictures published to the mailbox.</summary>
    public long Pictures => Interlocked.Read(ref _pictures);

    /// <summary>Access units decoded.</summary>
    public long Decoded => Interlocked.Read(ref _decoded);

    /// <summary>Access units that carried a new picture (<see cref="RepeatClassifier"/>).</summary>
    public long NewPictures => Interlocked.Read(ref _news);

    /// <summary>Access units that repeated the picture before.</summary>
    public long RepeatedPictures => Interlocked.Read(ref _repeated);

    /// <summary>The rhythm of new pictures the classifier follows.</summary>
    public PictureCadence Cadence => (PictureCadence)Volatile.Read(ref _cadence);

    public int Width => Volatile.Read(ref _width);

    public int Height => Volatile.Read(ref _height);

    public int DeepestQueue => Volatile.Read(ref _deepestQueue);

    /// <summary>Where a listener sees the units and a key frame is asked for.</summary>
    public AccessUnitTap Tap => _tap;

    /// <summary>The time every decode took, added up, the hand-off included.</summary>
    public TimeSpan DecodeTotal => Duration(Interlocked.Read(ref _decodeTicks));

    /// <summary>The time every hand-off of a decoded picture to the renderer took on this thread, added up.</summary>
    public TimeSpan HandOffTotal => Duration(Interlocked.Read(ref _handOffTicks));

    /// <summary>Why the worker stopped decoding, when the decoder threw; null while it runs.</summary>
    public string? Problem => Volatile.Read(ref _problem);

    public void Start()
    {
        _started = true;
        _thread.Start();
    }

    /// <summary>Queues a copy of a whole access unit; <paramref name="key"/> when it holds a key frame (an IDR).</summary>
    public void Enqueue(ReadOnlySpan<byte> unit, uint timestamp, bool key)
    {
        if (_queue.IsAddingCompleted || Problem is not null)
        {
            return;
        }

        _queue.Add((unit.ToArray(), timestamp, key));
        Volatile.Write(ref _deepestQueue, Math.Max(_deepestQueue, _queue.Count));
    }

    public TimeSpan AverageDecode()
    {
        long decoded = Decoded;

        if (decoded == 0)
        {
            return TimeSpan.Zero;
        }

        return Duration(Interlocked.Read(ref _decodeTicks) / decoded);
    }

    /// <summary>The slowest decode since the worker started.</summary>
    public TimeSpan WorstDecode()
    {
        return Duration(Interlocked.Read(ref _worstTicks));
    }

    /// <summary>The slowest decode since the last call, for a stats window.</summary>
    public TimeSpan TakeWindowWorst()
    {
        return Duration(Interlocked.Exchange(ref _windowWorstTicks, 0));
    }

    /// <summary>Ends the worker once the queued units are decoded; the decoder stays its owner's.</summary>
    public void Dispose()
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.CompleteAdding();
        }

        if (_started)
        {
            _thread.Join();
        }

        _queue.Dispose();
    }

    /// <summary>A decoder that threw ends the worker: the reason waits here, and the stream fails with it.</summary>
    private void Run()
    {
        try
        {
            DecodeAll();
        }
        catch (Exception error)
        {
            Volatile.Write(ref _problem, error.Message);
        }
    }

    private void DecodeAll()
    {
        foreach (var (unit, timestamp, key) in _queue.GetConsumingEnumerable())
        {
            long start = _clock.GetTimestamp();
            _tap.Offer(unit, timestamp, key);
            var kind = Classify(unit.Length, key);
            _gpu?.Prepare();
            _decoder.Decode(unit, ReadOnlySpan<byte>.Empty, timestamp);

            long decoded = _clock.GetTimestamp();
            HandOff(kind, decoded, timestamp);

            long end = _clock.GetTimestamp();
            Interlocked.Add(ref _handOffTicks, end - decoded);
            Count(end - start);
        }
    }

    // The decoded picture to the renderer: on the GPU while the decoder shares, else read back into the mailbox, where
    // the renderer shows the newest.
    private void HandOff(PictureKind kind, long decoded, uint timestamp)
    {
        if (_gpu is not null && _gpu.IsSharing)
        {
            if (_gpu.Publish(kind, decoded, timestamp, out var width, out var height))
            {
                Volatile.Write(ref _width, width);
                Volatile.Write(ref _height, height);
                Interlocked.Increment(ref _pictures);
            }

            return;
        }

        while (_decoder.TakePicture(_mailbox.Back))
        {
            Volatile.Write(ref _width, _mailbox.Back.Width);
            Volatile.Write(ref _height, _mailbox.Back.Height);
            _mailbox.Publish(decoded);
            Interlocked.Increment(ref _pictures);
        }
    }

    /// <summary>
    /// The sizes in bytes of the latest access units, oldest first, into <paramref name="sizes"/> (at least
    /// <see cref="SizeHistory"/> long); how many there are. For the log: read while the worker writes, a size may be a
    /// unit newer than its neighbours.
    /// </summary>
    public int RecentSizes(Span<int> sizes)
    {
        int written = Volatile.Read(ref _sizesWritten);
        int count = Math.Min(written, SizeHistory);

        for (int index = 0; index < count; index++)
        {
            sizes[index] = Volatile.Read(ref _sizes[(written - count + index) % SizeHistory]);
        }

        return count;
    }

    // New or repeat, by the unit's size and the cadence; counted for the stats, the size kept for the log.
    private PictureKind Classify(int bytes, bool key)
    {
        Volatile.Write(ref _sizes[_sizesWritten % SizeHistory], bytes);
        Volatile.Write(ref _sizesWritten, _sizesWritten + 1);

        var kind = _repeats.Classify(bytes, key);
        Volatile.Write(ref _cadence, (int)_repeats.Cadence);

        if (kind == PictureKind.Repeat)
        {
            Interlocked.Increment(ref _repeated);
        }
        else
        {
            Interlocked.Increment(ref _news);
        }

        return kind;
    }

    private void Count(long spent)
    {
        Interlocked.Add(ref _decodeTicks, spent);
        Interlocked.Increment(ref _decoded);

        Interlocked.Exchange(ref _worstTicks, Math.Max(Interlocked.Read(ref _worstTicks), spent));
        Interlocked.Exchange(ref _windowWorstTicks, Math.Max(Interlocked.Read(ref _windowWorstTicks), spent));
    }

    private TimeSpan Duration(long ticks)
    {
        return TimeSpan.FromSeconds((double)ticks / _clock.TimestampFrequency);
    }
}
