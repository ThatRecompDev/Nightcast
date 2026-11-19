// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_QUERY_DATA_TIMESTAMP_DISJOINT (d3d11.h): the GPU clock's ticks per second, and whether it jumped.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TimestampDisjointData
{
    public ulong Frequency;
    public int Disjoint;
}
