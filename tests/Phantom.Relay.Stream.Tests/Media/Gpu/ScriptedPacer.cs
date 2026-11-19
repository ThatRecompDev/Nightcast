// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Stream.Tests.Media.Gpu;

/// <summary>A pacer that shows what the test says and remembers the last group's size.</summary>
public sealed class ScriptedPacer : IPicturePacer
{
    public int Next { get; set; } = GpuPictureSlots.Hold;

    public int LastCount { get; private set; }

    public void OnGroup(long group, int count)
    {
        LastCount = count;
    }

    public int OnRefresh()
    {
        return Next;
    }
}
