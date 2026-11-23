// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>Win32's MONITORINFO: the monitor's whole area and its work area; <see cref="Size"/> is set before the call.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MonitorInfo
{
    public int Size;
    public Win32Rect Monitor;
    public Win32Rect Work;
    public uint Flags;
}
