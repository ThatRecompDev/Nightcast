// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// The decoded stereo waiting to play, read out at any ratio by linear interpolation between neighbouring frames. At
/// a ratio of exactly 1 and a whole position it copies the frames as they are. Audio thread only; its buffer is made
/// once.
/// </summary>
internal sealed class LinearResampler
{
    /// <summary>The frame at the read position and the one after it, which interpolation reads.</summary>
    private const int InterpolationFrames = 2;

    private const int Channels = OpusStreamFormat.Channels;

    private readonly float[] _samples;
    private readonly int _capacity;
    private int _count;
    private double _position;

    public LinearResampler(int capacityFrames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacityFrames, InterpolationFrames);

        _capacity = capacityFrames;
        _samples = new float[capacityFrames * Channels];
    }

    /// <summary>The frames held, the one being read included.</summary>
    public int Count => _count;

    /// <summary>The frames still ahead of the read position.</summary>
    public double Buffered => _count - _position;

    /// <summary>The frames it must hold to render <paramref name="frames"/> at <paramref name="step"/>.</summary>
    [HotPath]
    public int FramesNeeded(int frames, double step)
    {
        return (int)(_position + ((frames - 1) * step)) + InterpolationFrames;
    }

    [HotPath]
    public void Append(ReadOnlySpan<float> interleaved)
    {
        var frames = Math.Min(interleaved.Length / Channels, _capacity - _count);
        interleaved[..(frames * Channels)].CopyTo(_samples.AsSpan(_count * Channels));
        _count += frames;
    }

    [HotPath]
    public void AppendSilence(int frames)
    {
        var count = Math.Clamp(frames, 0, _capacity - _count);
        _samples.AsSpan(_count * Channels, count * Channels).Clear();
        _count += count;
    }

    /// <summary>Renders <paramref name="frames"/> frames, stepping <paramref name="step"/> input frames for each.</summary>
    [HotPath]
    public void Render(Span<float> output, int frames, double step)
    {
        var position = _position;

        for (var frame = 0; frame < frames; frame++)
        {
            var index = (int)position;
            var fraction = (float)(position - index);
            var at = index * Channels;
            var next = at + Channels;
            var to = frame * Channels;
            output[to] = _samples[at] + ((_samples[next] - _samples[at]) * fraction);

            output[to + 1] = _samples[at + 1] + ((_samples[next + 1] - _samples[at + 1]) * fraction);
            position += step;
        }

        // What was read past moves out; the frame being read stays first.
        var consumed = Math.Min((int)position, _count);
        _samples.AsSpan(consumed * Channels, (_count - consumed) * Channels).CopyTo(_samples);
        _count -= consumed;
        _position = position - consumed;
    }

    public void Clear()
    {
        _count = 0;
        _position = 0;
    }
}
