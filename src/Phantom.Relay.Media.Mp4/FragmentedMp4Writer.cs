// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// An H.264 stream and its Opus sound written into a fragmented MP4 as they come, without re-encoding (ISO/IEC 14496-12
/// and -15; Opus in ISOBMFF). The front (ftyp, moov with mvex) is written at once; then about every second
/// (<see cref="FragmentTicks"/>) a fragment (moof and mdat) of what came, flushed to the stream, so a file cut short
/// (a crash, the power) plays up to its last whole fragment. <see cref="Finish"/> adds the random access index (mfra)
/// and the movie's length (mehd). Only a fragment's samples are held in memory, never the file.
/// <para>
/// Video (track 1, 90 kHz): each access unit in Annex-B form with its time (90 kHz units, the first unit's time is the
/// movie's start; give key frames first) and its key mark, its NAL units given 4-byte lengths; a unit's duration is the
/// time to the next one, so each is held until the next comes. Sound (track 2, 48 kHz): each Opus packet with its time
/// on the movie's timeline (48 kHz units from the first video unit), its duration from its TOC; a gap in the times
/// is filled with packets a decoder conceals, so the sound stays in step with the video; a packet that comes late or
/// twice, or before the movie's start, is left out.
/// </para>
/// One thread uses it.
/// </summary>
public sealed class FragmentedMp4Writer
{
    /// <summary>The video's length in a fragment: one second of the 90 kHz clock.</summary>
    public const int FragmentTicks = Mp4VideoTrack.Timescale;

    /// <summary>The sound's length that makes a fragment on its own, when no video comes: two seconds.</summary>
    public const int AudioAloneSamples = Mp4AudioTrack.Timescale * 2;

    /// <summary>The last unit's duration when no other gave one: a frame at 60 a second.</summary>
    public const int NominalVideoTicks = Mp4VideoTrack.Timescale / 60;

    /// <summary>The longest gap in the sound filled with concealed packets: a minute; beyond it the sound runs early.</summary>
    public const long MaxConcealedSamples = Mp4AudioTrack.Timescale * 60L;

    private const int HeaderCapacity = 64 * 1024;

    // A packet that starts more than half its length before where the last one ended came late or twice.
    private const int LateFraction = 2;
    private const int MillisecondsPerSecond = 1000;

    private readonly Stream _output;
    private readonly long _start;
    private readonly long _durationAt;
    private readonly BoxBuffer _header = new(HeaderCapacity);
    private readonly SampleRun _video = new();
    private readonly SampleRun? _audio;
    private readonly FragmentIndex _index = new();
    private long _bytes;
    private int _fragments;
    private bool _videoStarted;
    private bool _holding;
    private int _heldSize;
    private long _heldTime;
    private bool _heldKey;
    private uint _lastDuration = NominalVideoTicks;
    private int _keyInRun = -1;
    private long _keyTime;
    private bool _audioStarted;
    private long _audioNext;
    private long _videoSamples;
    private long _keyFrames;
    private long _audioPackets;
    private long _concealed;
    private long _skipped;
    private bool _finished;

    /// <summary>
    /// A file of <paramref name="video"/> and <paramref name="audio"/> (null: video alone) written into
    /// <paramref name="output"/> from its position; its front is written now. A seekable stream also gets the movie's
    /// length when it finishes.
    /// </summary>
    public FragmentedMp4Writer(Stream output, Mp4VideoTrack video, Mp4AudioTrack? audio)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(video);

        _output = output;
        _start = Position(output);

        if (audio is not null)
        {
            _audio = new SampleRun();
        }

        _durationAt = _start + InitSegment.Write(_header, video, audio);
        Emit(_header.Written);
        _output.Flush();
    }

    /// <summary>Whether <see cref="Finish"/> was called.</summary>
    public bool IsFinished => _finished;

    /// <summary>What was written so far.</summary>
    public Mp4WriteCounts Counts => new(
        _videoSamples,
        _keyFrames,
        _audioPackets,
        _concealed,
        _skipped,
        _fragments,
        _bytes,
        Duration());

    /// <summary>Writes the access unit <paramref name="accessUnit"/> (Annex-B) at <paramref name="time"/> (90 kHz).</summary>
    public void WriteVideo(ReadOnlySpan<byte> accessUnit, long time, bool key)
    {
        ObjectDisposedException.ThrowIf(_finished, this);

        if (!_videoStarted)
        {
            _video.BaseTime = time;
            _videoStarted = true;
        }

        if (_holding)
        {
            Complete(time);
        }

        var size = LengthPrefixedUnits.Write(accessUnit, _video.Data);

        if (size > 0)
        {
            (_heldSize, _heldTime, _heldKey, _holding) = (size, time, key, true);
        }

        if (_video.Duration >= FragmentTicks)
        {
            Flush();
        }
    }

    /// <summary>Writes the Opus packet <paramref name="packet"/> at <paramref name="time"/> (48 kHz, on the movie's timeline).</summary>
    public void WriteAudio(ReadOnlySpan<byte> packet, long time)
    {
        ObjectDisposedException.ThrowIf(_finished, this);

        var samples = OpusPackets.Samples(packet);

        if (_audio is null || samples <= 0 || time < 0 || (_audioStarted && time < _audioNext - (samples / LateFraction)))
        {
            _skipped++;

            return;
        }

        if (!_audioStarted)
        {
            (_audio.BaseTime, _audioNext, _audioStarted) = (time, time, true);
        }

        Conceal(_audio, packet, time);
        _audio.Data.Bytes(packet);
        _audio.Add(packet.Length, (uint)samples, MovieFragment.SyncFlags);
        _audioNext += samples;
        _audioPackets++;

        if (_audio.Duration >= AudioAloneSamples)
        {
            Flush();
        }
    }

    /// <summary>
    /// The last unit (with the duration of the one before), the last fragment, the index and the movie's length; the
    /// stream is flushed and stays open, the caller's. Nothing more is written after it.
    /// </summary>
    public Mp4WriteCounts Finish()
    {
        if (_finished)
        {
            return Counts;
        }

        if (_holding)
        {
            Complete(_heldTime + _lastDuration);
        }

        Flush();
        WriteIndexAndLength();
        _output.Flush();
        _finished = true;

        return Counts;
    }

    private static long Position(Stream output)
    {
        if (output.CanSeek)
        {
            return output.Position;
        }

        return 0;
    }

    // The held unit is complete: it lasts until time.
    private void Complete(long time)
    {
        var duration = (uint)Math.Clamp(time - _heldTime, 1, uint.MaxValue);
        var flags = MovieFragment.NonSyncFlags;

        if (_heldKey)
        {
            flags = MovieFragment.SyncFlags;
            _keyFrames++;
        }

        if (_heldKey && _keyInRun < 0)
        {
            (_keyInRun, _keyTime) = (_video.Count, _heldTime);
        }

        _video.Add(_heldSize, duration, flags);
        _lastDuration = duration;
        _holding = false;
        _videoSamples++;
    }

    // A gap before time of a frame or more: the packets a decoder conceals, each a frame of packet's configuration.
    private void Conceal(SampleRun audio, ReadOnlySpan<byte> packet, long time)
    {
        var concealment = OpusPackets.ConcealmentOf(packet);
        var frame = OpusPackets.Samples(new ReadOnlySpan<byte>(in concealment));
        var limit = _audioNext + MaxConcealedSamples;

        while (time - _audioNext >= frame && _audioNext < limit)
        {
            audio.Data.U8(concealment);
            audio.Add(sizeof(byte), (uint)frame, MovieFragment.SyncFlags);
            _audioNext += frame;
            _concealed++;
        }

        // A gap longer than the limit is not filled: the timeline goes on from this packet.
        if (time > _audioNext)
        {
            _audioNext = time;
        }
    }

    // A fragment of the complete samples, written and flushed; its random access points kept for the index.
    private void Flush()
    {
        var audioCount = _audio?.Count ?? 0;

        if (_video.Count == 0 && audioCount == 0)
        {
            return;
        }

        var fragmentAt = _start + _bytes;
        _fragments++;
        MovieFragment.Write(_header, _fragments, _video, _audio);
        AddPoints(fragmentAt, audioCount);
        Emit(_header.Written);
        Emit(_video.Data.Written[.._video.CompleteLength]);

        if (_audio is not null)
        {
            Emit(_audio.Data.Written[.._audio.CompleteLength]);
            _audio.Clear();
        }

        _video.Clear();
        _keyInRun = -1;
        _output.Flush();
    }

    private void AddPoints(long fragmentAt, int audioCount)
    {
        var audioTraf = 1;

        if (_video.Count > 0)
        {
            audioTraf++;
        }

        if (_keyInRun >= 0)
        {
            _index.AddVideo(new RandomAccessPoint(_keyTime, fragmentAt, 1, _keyInRun + 1));
        }

        if (audioCount > 0)
        {
            _index.AddAudio(new RandomAccessPoint(_audio!.BaseTime, fragmentAt, audioTraf, 1));
        }
    }

    // mfra at the end, then the movie's length into mehd, where the stream can seek back to it.
    private void WriteIndexAndLength()
    {
        if (!_output.CanSeek)
        {
            return;
        }

        _index.Write(_header, _audio is not null);
        Emit(_header.Written);

        var end = _output.Position;
        Span<byte> length = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)Duration().TotalMilliseconds);
        _output.Position = _durationAt;
        _output.Write(length);
        _output.Position = end;
    }

    // The movie's length: where its longer track ends.
    private TimeSpan Duration()
    {
        var video = TimeSpan.Zero;
        var audio = TimeSpan.Zero;

        if (_videoStarted)
        {
            video = TimeSpan.FromMilliseconds(_video.EndTime * (double)MillisecondsPerSecond / Mp4VideoTrack.Timescale);
        }

        if (_audio is not null && _audioStarted)
        {
            audio = TimeSpan.FromMilliseconds(_audio.EndTime * (double)MillisecondsPerSecond / Mp4AudioTrack.Timescale);
        }

        return TimeSpan.FromTicks(Math.Max(video.Ticks, audio.Ticks));
    }

    private void Emit(ReadOnlySpan<byte> bytes)
    {
        _output.Write(bytes);
        _bytes += bytes.Length;
    }
}
