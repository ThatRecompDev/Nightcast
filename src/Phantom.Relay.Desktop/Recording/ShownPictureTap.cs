// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The decoder's tap for the pictures shown, for the head's life: it keeps the decoder's newest ring and, while a
/// recording runs (<see cref="Attach"/>), hands each shown picture's texture to its <see cref="ShownRecorder"/>. A
/// picture of an older ring than the newest (the ring was just made again) is let go. Pictures read back to the CPU never
/// come this way. Allocates only when the decoder makes a ring.
/// </summary>
/// <remarks>
/// The decoder keeps its two rings before the newest open, so a texture handed on stays valid while the render thread
/// copies from it, unless the ring is made again three times within that copy.
/// </remarks>
internal sealed class ShownPictureTap : IShownPictureTap
{
    private ShownRing? _ring;
    private ShownRecorder? _target;

    /// <summary>Whether the decoder handed its pictures over on the GPU yet.</summary>
    public bool HasRing => Volatile.Read(ref _ring) is not null;

    public void UseRing(int generation, SharedSurfaceRing ring, nint device)
    {
        Volatile.Write(ref _ring, new ShownRing(generation, ring, device));
    }

    /// <summary>The shown pictures go to <paramref name="target"/> from the next refresh on.</summary>
    public void Attach(ShownRecorder target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Volatile.Write(ref _target, target);
    }

    /// <summary>The shown pictures go nowhere from the next refresh on.</summary>
    public void Detach()
    {
        Volatile.Write(ref _target, null);
    }

    [HotPath]
    public void Shown(in GpuPicture picture)
    {
        var target = Volatile.Read(ref _target);

        if (target is null)
        {
            return;
        }

        var ring = Volatile.Read(ref _ring);

        if (ring is null || ring.Generation != picture.Generation)
        {
            return;
        }

        target.Shown(ring.Device, ring.Ring.Texture(picture.Slot), picture.Width, picture.Height);
    }
}
