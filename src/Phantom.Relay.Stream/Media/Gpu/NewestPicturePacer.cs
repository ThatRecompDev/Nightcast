// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// The pacing without frame generation: the newest group's last picture, at the first refresh after it is ready. For
/// latency: a picture is never held back, and one the renderer had no refresh for is simply replaced.
/// </summary>
public sealed class NewestPicturePacer : IPicturePacer
{
    private int _next = GpuPictureSlots.Hold;

    public void OnGroup(long group, int count)
    {
        _next = count - 1;
    }

    [HotPath]
    public int OnRefresh()
    {
        var next = _next;
        _next = GpuPictureSlots.Hold;

        return next;
    }
}
