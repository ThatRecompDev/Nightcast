// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Immutable;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// The textures a decoder hands its pictures over in, as the renderer opens them once: the shared handle of each slot
/// (on Windows a DXGI NT handle of an NV12 texture), the shared handle of the fence that says when a slot is written,
/// and the slots' size in texels. The producer keeps the handles open for its own life; a new
/// <see cref="Generation"/> replaces the ring (the stream's size changed), and pictures name the generation they are in.
/// </summary>
public sealed record GpuPictureRing(int Generation, ImmutableArray<nint> Slots, nint Fence, int TextureWidth, int TextureHeight);
