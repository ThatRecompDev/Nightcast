// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// One track's samples waiting for the next fragment: their bytes, one after another (<see cref="Data"/>), and their
/// sizes, durations and flags. Bytes may follow the samples that are complete (a video unit held until the next gives
/// its duration); <see cref="Clear"/> keeps those for the next fragment. Its time (<see cref="BaseTime"/>) is the
/// first sample's decoding time, the next fragment's tfdt. One thread; its buffers grow to a fragment's size and stay.
/// </summary>
internal sealed class SampleRun
{
    private const int InitialSamples = 128;
    private const int InitialBytes = 1 << 20;
    private const int Growth = 2;

    private Mp4Sample[] _samples = new Mp4Sample[InitialSamples];

    /// <summary>The samples' bytes, and after them any bytes of a sample not yet complete.</summary>
    public BoxBuffer Data { get; } = new(InitialBytes);

    /// <summary>The complete samples.</summary>
    public int Count { get; private set; }

    /// <summary>The bytes of the complete samples, the first <see cref="CompleteLength"/> of <see cref="Data"/>.</summary>
    public int CompleteLength { get; private set; }

    /// <summary>The first sample's decoding time, in the track's units.</summary>
    public long BaseTime { get; set; }

    /// <summary>The complete samples' durations together.</summary>
    public long Duration { get; private set; }

    /// <summary>When the run ends: the next sample's decoding time.</summary>
    public long EndTime => BaseTime + Duration;

    /// <summary>The complete samples.</summary>
    public ReadOnlySpan<Mp4Sample> Samples => _samples.AsSpan(0, Count);

    /// <summary>The next <paramref name="size"/> bytes of <see cref="Data"/> make a sample of <paramref name="duration"/>.</summary>
    public void Add(int size, uint duration, uint flags)
    {
        if (Count == _samples.Length)
        {
            Array.Resize(ref _samples, _samples.Length * Growth);
        }

        _samples[Count] = new Mp4Sample(size, duration, flags);
        Count++;
        CompleteLength += size;
        Duration += duration;
    }

    /// <summary>The complete samples were written: the run starts again where they ended, with any bytes after them.</summary>
    public void Clear()
    {
        Data.DropBefore(CompleteLength);
        BaseTime += Duration;
        Count = 0;
        CompleteLength = 0;
        Duration = 0;
    }
}
