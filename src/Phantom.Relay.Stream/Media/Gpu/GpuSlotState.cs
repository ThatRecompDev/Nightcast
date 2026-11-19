// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>Where a slot of a <see cref="GpuPictureSlots"/> is.</summary>
public enum GpuSlotState
{
    /// <summary>Nobody's: the producer may claim it.</summary>
    Free,

    /// <summary>The producer writes it.</summary>
    Writing,

    /// <summary>Written, in the newest group, waiting for its refresh.</summary>
    Pending,

    /// <summary>On screen: the renderer reads it every frame until another replaces it.</summary>
    Shown,
}
