// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>The decoder's current ring of shared textures, by its generation, and the device it is on.</summary>
internal sealed record ShownRing(int Generation, SharedSurfaceRing Ring, nint Device);
