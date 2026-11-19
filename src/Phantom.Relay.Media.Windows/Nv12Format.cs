// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// The decoder's NV12 output: the picture shown (<paramref name="Width"/> x <paramref name="Height"/>, the display
/// aperture), the coded height the UV plane follows in a system-memory buffer, and the bytes between rows there.
/// </summary>
internal readonly record struct Nv12Format(int Width, int Height, int CodedHeight, int Stride);
