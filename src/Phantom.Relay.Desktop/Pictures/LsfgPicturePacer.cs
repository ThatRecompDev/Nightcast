// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Lsfg.Pacing;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// The frame generation's pacing (<see cref="FramePacer"/>) behind the GPU path's pacer port: a group is the frames
/// generated between the last two new pictures, in display order, then the new picture, so generated step k is index
/// k - 1 and the real picture the last index. The first group (no picture before it) is the real picture alone.
/// </summary>
internal sealed class LsfgPicturePacer : IPicturePacer
{
    private readonly FramePacer _pacer;
    private int _count;

    public LsfgPicturePacer(FramePacer pacer)
    {
        _pacer = pacer;
    }

    [HotPath]
    public void OnGroup(long group, int count)
    {
        _count = count;
        _pacer.OnRealFrame(group);

        for (var step = 1; step < count; step++)
        {
            _pacer.OnGeneratedReady(group, step);
        }
    }

    [HotPath]
    public int OnRefresh()
    {
        var paced = _pacer.OnRefresh();

        if (paced.Kind == PacedFrameKind.Real)
        {
            return _count - 1;
        }

        if (paced.Kind == PacedFrameKind.Generated && paced.Step < _count)
        {
            return paced.Step - 1;
        }

        return GpuPictureSlots.Hold;
    }
}
