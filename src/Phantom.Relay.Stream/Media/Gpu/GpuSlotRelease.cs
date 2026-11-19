// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// What the renderer's last read of a slot ends at: its own shared fence (an NT handle it keeps for its life) and the
/// value it reaches once the GPU is done with the slot. The producer waits for it on the GPU before it writes the slot
/// again. A fence of 0 is nothing to wait for.
/// </summary>
public readonly record struct GpuSlotRelease(nint Fence, ulong Value);
