// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// A lock-free triple buffer of decoded pictures between the decode thread and the render thread. The decoder writes
/// into <see cref="Back"/> and publishes it; the renderer takes the newest published picture. Neither ever waits for
/// the other, and a picture the renderer had no time for is simply replaced: the newest frame wins, for latency.
/// </summary>
public sealed class PictureMailbox
{
    private const int IndexMask = 3;
    private const int Fresh = 4;
    private const int Third = 2;

    private readonly VideoFrame[] _frames = [new(), new(), new()];
    private int _back;
    private int _front = 1;
    private int _middle = Third;

    /// <summary>The decode thread's picture to write into.</summary>
    public VideoFrame Back => _frames[_back];

    /// <summary>The render thread's current picture.</summary>
    public VideoFrame Front => _frames[_front];

    /// <summary>True once the renderer has taken a picture.</summary>
    public bool HasPicture { get; private set; }

    /// <summary>The decode thread's side: hands over the picture just written and takes the free one.</summary>
    public void Publish()
    {
        int previous = Interlocked.Exchange(ref _middle, _back | Fresh);
        _back = previous & IndexMask;
    }

    /// <summary>The render thread's side: moves to the newest published picture when there is one.</summary>
    public bool TryTake()
    {
        if ((Volatile.Read(ref _middle) & Fresh) == 0)
        {
            return false;
        }

        int published = Interlocked.Exchange(ref _middle, _front);
        _front = published & IndexMask;
        HasPicture = true;

        return true;
    }
}
