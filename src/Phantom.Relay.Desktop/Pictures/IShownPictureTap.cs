// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// Sees each picture the GPU path puts on screen (a recorder of what Relay shows): the decode thread names each new
/// ring of shared textures (<see cref="UseRing"/>), the render thread each picture it takes for the screen
/// (<see cref="Shown"/>). Neither call may wait.
/// </summary>
internal interface IShownPictureTap
{
    /// <summary>Decode thread: the pictures of ring <paramref name="generation"/> are in <paramref name="ring"/> on <paramref name="device"/> (an ID3D11Device).</summary>
    void UseRing(int generation, SharedSurfaceRing ring, nint device);

    /// <summary>Render thread: <paramref name="picture"/> is on screen from this refresh.</summary>
    void Shown(in GpuPicture picture);
}
