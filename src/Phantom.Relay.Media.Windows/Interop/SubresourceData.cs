// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_SUBRESOURCE_DATA (d3d11.h): a resource's first contents and its row and slice pitches.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SubresourceData
{
    public nint Memory;
    public uint RowPitch;
    public uint SlicePitch;
}
