// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>Win32's RECT: the edges in screen pixels.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Win32Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;

    public readonly int Height => Bottom - Top;
}
