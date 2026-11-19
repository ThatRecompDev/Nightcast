// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_TEXTURE2D_DESC (d3d11.h), its sample description flattened into two fields.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Texture2DDescription
{
    public uint Width;
    public uint Height;
    public uint MipLevels;
    public uint ArraySize;
    public uint Format;
    public uint SampleCount;
    public uint SampleQuality;
    public uint Usage;
    public uint BindFlags;
    public uint CpuAccessFlags;
    public uint MiscFlags;
}
