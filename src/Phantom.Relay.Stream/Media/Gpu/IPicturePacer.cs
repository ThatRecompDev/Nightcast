// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// Says which picture of the newest group each display refresh shows. A group is what one decoded picture gives: the
/// picture alone, or with frame generation the frames between the one before and it, then it, in display order. Called
/// under the <see cref="GpuPictureSlots"/>'s lock, the group from the decode thread and the refresh from the render
/// thread, so an implementation needs no lock of its own.
/// </summary>
public interface IPicturePacer
{
    /// <summary>A group of <paramref name="count"/> pictures is ready now; it replaces what was pending of the one before.</summary>
    void OnGroup(long group, int count);

    /// <summary>The index in the newest group this refresh shows, or <see cref="GpuPictureSlots.Hold"/> to keep what is on screen.</summary>
    int OnRefresh();
}
