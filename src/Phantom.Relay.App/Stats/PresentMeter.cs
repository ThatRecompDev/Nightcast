// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.App.Stats;

/// <summary>
/// Adds up the render thread's timings until the stats are shown again (<see cref="Take"/>): each new picture's time
/// from decode to present, and each frame's CPU time for its picture. Render thread only; allocates nothing.
/// </summary>
public sealed class PresentMeter
{
    private TimeSpan _latencyTotal;
    private TimeSpan _latencyWorst;
    private TimeSpan _cpuTotal;
    private long _presented;
    private long _frames;
    private bool _onGpu;

    /// <summary>A new picture reached the screen <paramref name="decodeToPresent"/> after its decode, by the GPU path or not.</summary>
    [HotPath]
    public void Presented(TimeSpan decodeToPresent, bool onGpu)
    {
        _latencyTotal += decodeToPresent;
        _latencyWorst = TimeSpan.FromTicks(Math.Max(_latencyWorst.Ticks, decodeToPresent.Ticks));
        _presented++;
        _onGpu = onGpu;
    }

    /// <summary>A frame spent <paramref name="pictureCpu"/> of CPU getting its picture on screen.</summary>
    [HotPath]
    public void Frame(TimeSpan pictureCpu)
    {
        _cpuTotal += pictureCpu;
        _frames++;
    }

    /// <summary>The window's averages and worst, and a fresh window.</summary>
    public PresentStats Take()
    {
        var stats = new PresentStats(Average(_latencyTotal, _presented), _latencyWorst, Average(_cpuTotal, _frames), _onGpu);
        _latencyTotal = TimeSpan.Zero;
        _latencyWorst = TimeSpan.Zero;
        _cpuTotal = TimeSpan.Zero;
        _presented = 0;
        _frames = 0;

        return stats;
    }

    private static TimeSpan Average(TimeSpan total, long count)
    {
        if (count == 0)
        {
            return TimeSpan.Zero;
        }

        return total / count;
    }
}
