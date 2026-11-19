// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC (d3d11.h) for a 2D texture: the dimension, D3D11_TEX2D_VPOV (mip slice)
/// and the two words the union's largest member (D3D11_TEX2D_ARRAY_VPOV) adds.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VideoProcessorOutputViewDescription
{
    public uint ViewDimension;
    public uint MipSlice;
    public uint Unused0;
    public uint Unused1;
}
