// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// D3D11_UNORDERED_ACCESS_VIEW_DESC (d3d11.h) for a 2D texture: the format, the dimension, then the union's
/// D3D11_TEX2D_UAV (mip slice) and the two words the union's largest member adds.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UnorderedAccessViewDescription
{
    public uint Format;
    public uint ViewDimension;
    public uint MipSlice;
    public uint Unused0;
    public uint Unused1;
}
