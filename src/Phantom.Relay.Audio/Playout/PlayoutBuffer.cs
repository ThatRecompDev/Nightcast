// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// The audio thread's side of the player: it takes the packets the network thread queued, puts them in sequence
/// order, decodes the one due, rebuilds or conceals what is missing and resamples the result to the device by the
/// drift ratio. Everything here runs inside the device's render call: no lock, no allocation, no exception for
/// anything the stream does. Its counters are published for other threads with volatile writes.
/// </summary>
internal sealed class PlayoutBuffer
{
    /// <summary>The most frames rendered in one pass; a larger device buffer is rendered in passes.</summary>
    private const int ChunkFrames = 1024;

    /// <summary>How long it conceals, waiting for a packet, before it gives up and fills again.</summary>
    private const int MaxConcealmentMilliseconds = 100;

    /// <summary>A queue this many times the largest delay is a burst after a stall: it is cut back to the target.</summary>
    private const int BacklogFactor = 2;

    private const int Channels = OpusStreamFormat.Channels;

    private readonly IOpusFrameDecoder _decoder;
    private readonly PacketQueue _queue;
    private readonly ReorderWindow _window = new();
    private readonly DriftController _drift = new();
    private readonly ArrivalJitter _jitter;
    private readonly PlayoutTarget _target;
    private readonly LinearResampler _resampler;
    private readonly float[] _pcm = new float[OpusStreamFormat.MaxPacketSamples * Channels];
    private readonly double _nominalStep;
    private readonly double _samplesPerTick;
    private readonly int _maxConcealment;
    private readonly int _backlogLimit;
    private bool _hasNext;
    private bool _playing;
    private bool _underrun;
    private ushort _next;
    private ushort _highest;
    private uint _nextTimestamp;
    private uint _highestEnd;
    private int _packetSamples;
    private int _concealedRun;
    private long _underruns;
    private long _concealed;
    private long _fecRecoveries;
    private long _lateDrops;
    private long _played;
    private long _skipped;
    private long _resyncs;
    private double _publishedDelay;
    private double _publishedTarget;
    private double _publishedRatio;
    private int _publishedPlaying;

    public PlayoutBuffer(IOpusFrameDecoder decoder, PacketQueue queue, PlayoutSettings settings, int outputRate, long timestampFrequency)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputRate);

        _decoder = decoder;
        _queue = queue;
        _target = new PlayoutTarget(settings);
        _jitter = new ArrivalJitter(timestampFrequency);
        _nominalStep = (double)OpusStreamFormat.SampleRate / outputRate;
        _samplesPerTick = (double)OpusStreamFormat.SampleRate / timestampFrequency;

        // Room for a pass at the fastest ratio and one more packet than it needs.
        var passFrames = (ChunkFrames + 1) * _nominalStep * (1d + DriftController.MaxAdjustment);
        _resampler = new LinearResampler((int)Math.Ceiling(passFrames) + OpusStreamFormat.MaxPacketSamples);
        _maxConcealment = MaxConcealmentMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _backlogLimit = BacklogFactor * settings.MaxDelayMilliseconds * OpusStreamFormat.SamplesPerMillisecond;
        _publishedRatio = 1d;
    }

    public double DelaySamples => Volatile.Read(ref _publishedDelay);

    public double TargetSamples => Volatile.Read(ref _publishedTarget);

    public double Ratio => Volatile.Read(ref _publishedRatio);

    public bool Playing => Volatile.Read(ref _publishedPlaying) != 0;

    public long Underruns => Volatile.Read(ref _underruns);

    public long Concealed => Volatile.Read(ref _concealed);

    public long FecRecoveries => Volatile.Read(ref _fecRecoveries);

    public long LateDrops => Volatile.Read(ref _lateDrops);

    public long Played => Volatile.Read(ref _played);

    public long Skipped => Volatile.Read(ref _skipped);

    public long Resyncs => Volatile.Read(ref _resyncs);

    /// <summary>Fills <paramref name="frames"/> frames of <paramref name="interleaved"/>; <paramref name="now"/> is the clock's timestamp.</summary>
    [HotPath]
    public void Render(Span<float> interleaved, int frames, long now)
    {
        var done = 0;

        while (done < frames)
        {
            var count = Math.Min(ChunkFrames, frames - done);
            RenderPass(interleaved.Slice(done * Channels, count * Channels), count, now);
            done += count;
        }

        Publish();
    }

    [HotPath]
    private static void Count(ref long counter)
    {
        Volatile.Write(ref counter, counter + 1);
    }

    [HotPath]
    private void RenderPass(Span<float> output, int frames, long now)
    {
        var step = _nominalStep * _drift.Ratio;
        var advance = frames * step;
        Drain(now, advance);

        if (!_playing)
        {
            TryStart(advance);
        }

        if (!_playing)
        {
            output.Clear();
            _resampler.Clear();

            return;
        }

        TrimBacklog();
        var needed = _resampler.FramesNeeded(frames, step);

        while (_resampler.Count < needed)
        {
            Produce(needed);
        }

        _resampler.Render(output, frames, step);
        _target.Follow(_jitter.SpreadSamples, (int)advance);
        _drift.Update(_target.Samples, (int)advance);
    }

    /// <summary>Takes every packet the network thread queued since the last pass.</summary>
    [HotPath]
    private void Drain(long now, double advance)
    {
        while (_queue.TryTake(out var slot))
        {
            Accept(slot, now, advance);
        }
    }

    [HotPath]
    private void Accept(int slot, long now, double advance)
    {
        var sequence = _queue.Sequence(slot);
        _jitter.Observe(_queue.Arrival(slot), _queue.Timestamp(slot));

        if (!_hasNext)
        {
            _hasNext = true;
            _target.Initialize(_queue.Samples(slot));
            Anchor(slot);
        }

        var ahead = (short)(sequence - _next);

        if (ahead < -ReorderWindow.Capacity || ahead >= ReorderWindow.Capacity)
        {
            Resync(slot);
        }
        else if (ahead < 0 && !Rewinds(sequence))
        {
            Count(ref _lateDrops);
            _queue.Release(slot);

            return;
        }

        if (!_window.TryPlace(sequence, slot))
        {
            _queue.Release(slot);

            return;
        }

        NoteHighest(slot);
        ObserveHeadroom(slot, now, advance);
    }

    /// <summary>Before playing starts, a packet that came after a later one moves the start back to it.</summary>
    [HotPath]
    private bool Rewinds(ushort sequence)
    {
        if (_playing || (short)(_highest - sequence) >= ReorderWindow.Capacity)
        {
            return false;
        }

        _next = sequence;

        return true;
    }

    /// <summary>The sequence numbers jumped past the window: what waits is dropped and the buffer fills again.</summary>
    [HotPath]
    private void Resync(int slot)
    {
        for (var held = _window.TakeAny(); held != ReorderWindow.None; held = _window.TakeAny())
        {
            _queue.Release(held);
        }

        Count(ref _resyncs);
        _playing = false;
        _jitter.Reset();
        Anchor(slot);
    }

    [HotPath]
    private void Anchor(int slot)
    {
        _next = _queue.Sequence(slot);
        _highest = _next;
        _highestEnd = _queue.Timestamp(slot) + (uint)_queue.Samples(slot);
        _packetSamples = _queue.Samples(slot);
    }

    [HotPath]
    private void NoteHighest(int slot)
    {
        var sequence = _queue.Sequence(slot);

        if ((short)(sequence - _highest) >= 0)
        {
            _highest = sequence;
            _highestEnd = _queue.Timestamp(slot) + (uint)_queue.Samples(slot);
        }
    }

    /// <summary>
    /// How long before its first sample is due the packet arrived. The play position moves on a straight line between
    /// renders, so it is placed back at the arrival time: the measure does not depend on how arrivals and renders
    /// interleave.
    /// </summary>
    [HotPath]
    private void ObserveHeadroom(int slot, long now, double advance)
    {
        if (!_playing)
        {
            return;
        }

        var waited = (now - _queue.Arrival(slot)) * _samplesPerTick * _drift.Ratio;
        var ahead = (int)(_queue.Timestamp(slot) - _nextTimestamp) + _resampler.Buffered;
        var headroom = Math.Clamp(ahead - advance + waited, -_backlogLimit, _backlogLimit);
        _drift.Observe(headroom, _queue.Samples(slot));
    }

    /// <summary>Starts once a packet and the target's delay wait beyond what this pass reads.</summary>
    [HotPath]
    private void TryStart(double advance)
    {
        if (_window.Count == 0)
        {
            return;
        }

        _next = _window.Lowest(_next);
        _nextTimestamp = _queue.Timestamp(_window.Find(_next));

        if (QueuedSamples() < _target.Samples + _packetSamples + advance)
        {
            return;
        }

        _playing = true;
        _underrun = false;
        _concealedRun = 0;
        _drift.Restart();
    }

    /// <summary>A burst after a stall would hold the delay high for seconds: the oldest packets go instead.</summary>
    [HotPath]
    private void TrimBacklog()
    {
        if (QueuedSamples() <= _backlogLimit)
        {
            return;
        }

        while (_window.Count > 0 && QueuedSamples() > _target.Samples + _packetSamples)
        {
            SkipOldest();
        }

        _drift.Restart();
    }

    [HotPath]
    private void SkipOldest()
    {
        var slot = _window.Claim(_next);
        _next++;
        Count(ref _skipped);

        if (slot == ReorderWindow.None)
        {
            _nextTimestamp += (uint)_packetSamples;

            return;
        }

        _nextTimestamp = _queue.Timestamp(slot) + (uint)_queue.Samples(slot);
        _queue.Release(slot);
    }

    /// <summary>The sound from the next one due to the end of the latest packet, in samples.</summary>
    [HotPath]
    private int QueuedSamples()
    {
        if (_window.Count == 0)
        {
            return 0;
        }

        return Math.Max(0, (int)(_highestEnd - _nextTimestamp));
    }

    /// <summary>One packet's worth of sound: the packet due, its rebuild, a concealment, or silence once stopped.</summary>
    [HotPath]
    private void Produce(int needed)
    {
        if (!_playing)
        {
            _resampler.AppendSilence(needed - _resampler.Count);

            return;
        }

        var slot = _window.Claim(_next);

        if (slot != ReorderWindow.None)
        {
            PlayPacket(slot);

            return;
        }

        // A later packet is here, so the one due is lost; with none, the stream is late.
        if (_window.Count > 0)
        {
            RecoverLost();

            return;
        }

        ConcealUnderrun();
    }

    [HotPath]
    private void PlayPacket(int slot)
    {
        var samples = _queue.Samples(slot);
        var decoded = _decoder.Decode(_queue.Payload(slot), _pcm);
        _nextTimestamp = _queue.Timestamp(slot) + (uint)samples;
        _queue.Release(slot);
        _next++;
        _packetSamples = samples;

        _underrun = false;
        _concealedRun = 0;
        Count(ref _played);

        if (decoded < 0)
        {
            Count(ref _concealed);
            decoded = -decoded;
        }

        Append(decoded);
    }

    /// <summary>The packet due was lost: the next packet's FEC rebuilds it when it carries one, else it is concealed.</summary>
    [HotPath]
    private void RecoverLost()
    {
        var samples = _packetSamples;
        var following = _window.Find((ushort)(_next + 1));
        var decoded = 0;
        _next++;
        _nextTimestamp += (uint)samples;

        if (following != ReorderWindow.None)
        {
            decoded = _decoder.DecodeFec(_queue.Payload(following), samples, _pcm);
        }

        if (decoded > 0)
        {
            Count(ref _fecRecoveries);
            Append(decoded);

            return;
        }

        decoded = _decoder.DecodeLost(samples, _pcm);
        Count(ref _concealed);
        Append(decoded);
    }

    /// <summary>
    /// Nothing is here: the stream is late. It conceals a packet's worth without moving on, so the late packet still
    /// plays when it comes and the delay grows by what was concealed; the target grows too. After too long it stops
    /// and fills again.
    /// </summary>
    [HotPath]
    private void ConcealUnderrun()
    {
        if (!_underrun)
        {
            _underrun = true;
            Count(ref _underruns);
            _target.Grow(_packetSamples);
        }

        if (_concealedRun >= _maxConcealment)
        {
            _playing = false;

            return;
        }

        var decoded = _decoder.DecodeLost(_packetSamples, _pcm);
        _concealedRun += _packetSamples;
        Count(ref _concealed);
        Append(decoded);
    }

    [HotPath]
    private void Publish()
    {
        int playing = _playing ? 1 : 0;
        Volatile.Write(ref _publishedDelay, _drift.SmoothedDelay);
        Volatile.Write(ref _publishedTarget, _target.Samples);
        Volatile.Write(ref _publishedRatio, _drift.Ratio);
        Volatile.Write(ref _publishedPlaying, playing);
    }

    [HotPath]
    private void Append(int samples)
    {
        if (samples <= 0 || samples > OpusStreamFormat.MaxPacketSamples)
        {
            _resampler.AppendSilence(_packetSamples);

            return;
        }

        _resampler.Append(_pcm.AsSpan(0, samples * Channels));
    }
}
