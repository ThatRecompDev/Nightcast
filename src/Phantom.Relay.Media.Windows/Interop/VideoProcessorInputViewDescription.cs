// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC (d3d11.h) for a 2D texture: FourCC (0 takes the texture's format), the
/// dimension, then D3D11_TEX2D_VPIV (mip slice, array slice: a decoder surface is one slice of a texture array).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VideoProcessorInputViewDescription
{
    public uint FourCc;
    public uint ViewDimension;
    public uint MipSlice;
    public uint ArraySlice;
}
