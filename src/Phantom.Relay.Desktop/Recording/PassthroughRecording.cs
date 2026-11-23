// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Audio;
using Phantom.Relay.Media.Mp4;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// One recording into source.mp4 with the pure C# writer (<see cref="FragmentedMp4Writer"/>): the console's H.264 and
/// Opus as they came, no re-encoding, so it costs almost nothing (the Linux recorder, docs/architecture/recorder.md).
/// The decode thread offers each access unit (<see cref="Received(byte[], uint, bool)"/>: queued from the first key
/// frame on, the worker's own copy, never waited for) and the network thread each sound packet (copied into a ring
/// made in advance, from that key frame on). Its own thread writes both: the video's times from its RTP clock, the
/// sound's from its own RTP clock, started where its first packet arrived after the key frame (the two clocks are
/// unrelated, so arrival puts them in step). It writes <c>source.mp4.part</c>; <see cref="Finish"/> finishes it, renames
/// it to source.mp4 and writes the empty <c>done</c> file, in that order, so a watcher knows the recording is whole.
/// </summary>
internal sealed class PassthroughRecording : IAccessUnitListener, IAudioPacketListener, IDisposable
{
    /// <summary>The access units queued at most: about four seconds of 60 a second.</summary>
    public const int UnitCapacity = 256;

    /// <summary>The sound packets queued at most: about five seconds of the console's 10 ms packets.</summary>
    public const int PacketCapacity = 512;

    private const string NoKeyFrame = "no key frame came while it recorded";
    private const string NoParameterSets = "the first key frame carries no sequence and picture parameter sets";
    private const int FileBuffer = 1 << 16;

    private static readonly TimeSpan KeyFrameRetry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SoundTurn = TimeSpan.FromMilliseconds(20);

    private readonly RecordingQueue<SourceUnit> _units = new(UnitCapacity);
    private readonly AudioPacketRing _packets = new(PacketCapacity, OpusStreamFormat.MaxPayloadBytes);
    private readonly byte[] _packet = new byte[OpusStreamFormat.MaxPayloadBytes];
    private readonly RtpTimeline _video = new();
    private readonly string _path;
    private readonly int _width;
    private readonly int _height;
    private readonly AccessUnitTap _tap;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly Thread _thread;
    private FileStream? _file;
    private FragmentedMp4Writer? _writer;
    private long _origin;
    private int _started;
    private long _skipped;
    private bool _soundStarted;
    private long _soundOffset;
    private long _soundTicks;
    private uint _soundLast;
    private string? _problem;

    /// <summary>A recording of a <paramref name="width"/> x <paramref name="height"/> stream into <paramref name="folder"/>; <paramref name="tap"/> asks for key frames.</summary>
    public PassthroughRecording(string folder, int width, int height, AccessUnitTap tap, TimeProvider clock, ILogger log)
    {
        Folder = folder;
        _path = Path.Combine(folder, RecordingFolder.SourceFile);
        _width = width;
        _height = height;
        _tap = tap;
        _clock = clock;

        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "Relay recording" };
    }

    public string Folder { get; }

    /// <summary>Whether the first key frame came.</summary>
    public bool HasStarted => Volatile.Read(ref _started) != 0;

    /// <summary>When it came (the clock's timestamp); valid once <see cref="HasStarted"/>.</summary>
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

        _ = _units.TryAdd(new SourceUnit(unit, rtpTimestamp, key));
    }

    /// <summary>The network thread handed on a sound packet: copied from the first key frame on, never waited for.</summary>
    public void Received(ReadOnlySpan<byte> payload, uint timestamp)
    {
        if (HasStarted)
        {
            _ = _packets.TryAdd(payload, timestamp, _clock.GetTimestamp());
        }
    }

    /// <summary>No more comes (the taps let go first): the file finished, renamed and marked done; how it came out.</summary>
    public PassthroughOutcome Finish()
    {
        _units.Complete();

        if (_thread.IsAlive)
        {
            _thread.Join();
        }

        var problem = _problem;
        Mp4WriteCounts? counts = null;

        if (_writer is null)
        {
            problem ??= NoKeyFrame;
        }
        else
        {
            counts = Close();
        }

        return new PassthroughOutcome(counts, problem, Interlocked.Read(ref _skipped), _units.Dropped, _packets.Dropped);
    }

    /// <summary>The file let go of, finished or not (a recording that failed keeps its source.mp4.part).</summary>
    public void Dispose()
    {
        _file?.Dispose();
    }

    // The writer: each unit as it comes, the sound between; while none has started, a key frame asked for every second.
    private void Run()
    {
        try
        {
            WriteAll();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _problem = error.Message;
            _log.SourceRefused(error.Message);
        }
    }

    private void WriteAll()
    {
        var waited = TimeSpan.Zero;

        while (true)
        {
            if (_units.TryTake(out var unit, SoundTurn))
            {
                Write(unit);
                WriteSound();

                continue;
            }

            WriteSound();

            if (_units.IsCompleted)
            {
                return;
            }

            waited += SoundTurn;

            if (!HasStarted && waited >= KeyFrameRetry)
            {
                _tap.RequestKeyframe();
                waited = TimeSpan.Zero;
            }
        }
    }

    // The file opens at the first unit (a key frame: its parameter sets describe the stream).
    private void Write(in SourceUnit unit)
    {
        if (_writer is null && _problem is null)
        {
            Open(unit);
        }

        if (_writer is not null)
        {
            _ = _video.Next(unit.RtpTimestamp);
            _writer.WriteVideo(unit.Unit, _video.Ticks, unit.Key);
        }
    }

    private void Open(in SourceUnit unit)
    {
        var track = Mp4VideoTrack.FromKeyFrame(unit.Unit, _width, _height);

        if (track is null)
        {
            _problem = NoParameterSets;
            _log.SourceRefused(NoParameterSets);

            return;
        }

        _file = new FileStream(_path + RecordingFolder.PartSuffix, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, FileBuffer);
        _writer = new FragmentedMp4Writer(_file, track, new Mp4AudioTrack(OpusStreamFormat.Channels, 0));
    }

    // The packets queued so far, once the file is open: each at its place on the movie's 48 kHz timeline.
    private void WriteSound()
    {
        if (_writer is null)
        {
            return;
        }

        int length;

        while ((length = _packets.TryTake(_packet, out var timestamp, out var arrival)) >= 0)
        {
            _writer.WriteAudio(_packet.AsSpan(0, length), SoundTime(timestamp, arrival));
        }
    }

    // The first packet's place is where it arrived after the key frame; the rest follow its RTP clock (wrap-safe).
    private long SoundTime(uint timestamp, long arrival)
    {
        if (!_soundStarted)
        {
            var after = _clock.GetElapsedTime(Origin, arrival).TotalSeconds;
            _soundOffset = Math.Max(0, (long)(after * Mp4AudioTrack.Timescale));
            (_soundLast, _soundStarted) = (timestamp, true);
        }

        _soundTicks += unchecked((int)(timestamp - _soundLast));
        _soundLast = timestamp;

        return _soundOffset + _soundTicks;
    }

    // The last fragment and the index, then source.mp4.part renamed to source.mp4, then the done file.
    private Mp4WriteCounts Close()
    {
        var counts = _writer!.Finish();
        _file!.Dispose();
        File.Move(_path + RecordingFolder.PartSuffix, _path);
        RecordingFolder.MarkDone(Folder);

        return counts;
    }
}
