// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Concurrent;
using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Decodes access units on a thread of its own, so the network loop never waits on the decoder: units are queued as
/// they complete, and the worker times each decode. The decoder is the head's choice (openh264 in software today, a
/// hardware decoder later) and stays the head's to dispose. Each picture goes straight into the
/// <see cref="PictureMailbox"/>; the counters are safe to read from any thread.
/// </summary>
public sealed class DecodeWorker : IDisposable
{
    private readonly BlockingCollection<(byte[] Unit, uint Timestamp)> _queue = [];
    private readonly IVideoDecoder _decoder;
    private readonly PictureMailbox _mailbox;
    private readonly TimeProvider _clock;
    private readonly Thread _thread;

    private long _pictures;
    private long _decoded;
    private long _decodeTicks;
    private long _worstTicks;
    private long _windowWorstTicks;
    private int _deepestQueue;
    private int _width;
    private int _height;
    private bool _started;
    private string? _problem;

    public DecodeWorker(IVideoDecoder decoder, PictureMailbox mailbox, TimeProvider clock)
    {
        _decoder = decoder;
        _mailbox = mailbox;
        _clock = clock;
        _thread = new Thread(Run) { IsBackground = true, Name = "Relay video decode" };
    }

    /// <summary>Pictures published to the mailbox.</summary>
    public long Pictures => Interlocked.Read(ref _pictures);

    /// <summary>Access units decoded.</summary>
    public long Decoded => Interlocked.Read(ref _decoded);

    public int Width => Volatile.Read(ref _width);

    public int Height => Volatile.Read(ref _height);

    public int DeepestQueue => Volatile.Read(ref _deepestQueue);

    /// <summary>The time every decode took, added up.</summary>
    public TimeSpan DecodeTotal => Duration(Interlocked.Read(ref _decodeTicks));

    /// <summary>Why the worker stopped decoding, when the decoder threw; null while it runs.</summary>
    public string? Problem => Volatile.Read(ref _problem);

    public void Start()
    {
        _started = true;
        _thread.Start();
    }

    /// <summary>Queues a copy of a whole access unit.</summary>
    public void Enqueue(ReadOnlySpan<byte> unit, uint timestamp)
    {
        if (_queue.IsAddingCompleted || Problem is not null)
        {
            return;
        }

        _queue.Add((unit.ToArray(), timestamp));
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
        foreach (var (unit, timestamp) in _queue.GetConsumingEnumerable())
        {
            long start = _clock.GetTimestamp();
            _decoder.Decode(unit, ReadOnlySpan<byte>.Empty, timestamp);

            // Each picture goes straight to the mailbox: the renderer shows the newest.
            while (_decoder.TakePicture(_mailbox.Back))
            {
                Volatile.Write(ref _width, _mailbox.Back.Width);
                Volatile.Write(ref _height, _mailbox.Back.Height);
                _mailbox.Publish();
                Interlocked.Increment(ref _pictures);
            }

            long spent = _clock.GetTimestamp() - start;
            Interlocked.Add(ref _decodeTicks, spent);
            Interlocked.Increment(ref _decoded);

            Interlocked.Exchange(ref _worstTicks, Math.Max(Interlocked.Read(ref _worstTicks), spent));
            Interlocked.Exchange(ref _windowWorstTicks, Math.Max(Interlocked.Read(ref _windowWorstTicks), spent));
        }
    }

    private TimeSpan Duration(long ticks)
    {
        return TimeSpan.FromSeconds((double)ticks / _clock.TimestampFrequency);
    }
}
