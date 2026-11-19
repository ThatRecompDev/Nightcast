// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// D3D11_SHADER_RESOURCE_VIEW_DESC (d3d11.h) for a 2D texture: the format, the dimension, then the union's
/// D3D11_TEX2D_SRV (most detailed mip, mip count) and the two words the union's largest member adds.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShaderResourceViewDescription
{
    public uint Format;
    public uint ViewDimension;
    public uint MostDetailedMip;
    public uint MipLevels;
    public uint Unused0;
    public uint Unused1;
}
