// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Concurrent;
using Phantom.Engine.Media.Video;
using Phantom.Engine.Media.Vpx;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// Decodes access units on a thread of its own, so the network loop never waits on the decoder: units are queued as
/// they complete, and the worker times each decode. Counters are read once the run is over.
/// </summary>
public sealed class DecodeWorker : IDisposable
{
    private readonly BlockingCollection<(byte[] Unit, uint Timestamp)> _queue = [];
    private readonly H264Decoder _decoder;
    private readonly VideoFrame _picture = new();
    private readonly TimeProvider _clock;
    private readonly Thread _thread;

    private long _pictures;
    private long _decoded;
    private long _decodeTicks;
    private long _worstTicks;
    private int _deepestQueue;

    public DecodeWorker(H264Decoder decoder, TimeProvider clock)
    {
        _decoder = decoder;
        _clock = clock;
        _thread = new Thread(Run) { IsBackground = true, Name = "Relay video decode" };
    }

    public long Pictures => Interlocked.Read(ref _pictures);

    public long Decoded => Interlocked.Read(ref _decoded);

    public int Width => _picture.Width;

    public int Height => _picture.Height;

    public int DeepestQueue => _deepestQueue;

    public void Start()
    {
        _thread.Start();
    }

    /// <summary>Queues a copy of a whole access unit.</summary>
    public void Enqueue(ReadOnlySpan<byte> unit, uint timestamp)
    {
        _queue.Add((unit.ToArray(), timestamp));
        _deepestQueue = Math.Max(_deepestQueue, _queue.Count);
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

    public TimeSpan WorstDecode()
    {
        return Duration(Interlocked.Read(ref _worstTicks));
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join();
        _queue.Dispose();
        _decoder.Dispose();
    }

    private void Run()
    {
        foreach (var (unit, timestamp) in _queue.GetConsumingEnumerable())
        {
            long start = _clock.GetTimestamp();
            _decoder.Decode(unit, timestamp);

            while (_decoder.TakePicture(_picture))
            {
                Interlocked.Increment(ref _pictures);
            }

            long spent = _clock.GetTimestamp() - start;
            Interlocked.Add(ref _decodeTicks, spent);
            Interlocked.Increment(ref _decoded);
            Interlocked.Exchange(ref _worstTicks, Math.Max(Interlocked.Read(ref _worstTicks), spent));
        }
    }

    private TimeSpan Duration(long ticks)
    {
        return TimeSpan.FromSeconds((double)ticks / _clock.TimestampFrequency);
    }
}
