// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_MAPPED_SUBRESOURCE (d3d11.h): where a mapped texture's rows are and how far apart.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MappedSubresource
{
    public nint Data;
    public uint RowPitch;
    public uint DepthPitch;
}
