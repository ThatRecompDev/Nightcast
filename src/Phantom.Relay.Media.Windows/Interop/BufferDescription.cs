// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_BUFFER_DESC (d3d11.h): a buffer's size, usage and bindings (a constant buffer here).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BufferDescription
{
    public uint ByteWidth;
    public uint Usage;
    public uint BindFlags;
    public uint CpuAccessFlags;
    public uint MiscFlags;
    public uint StructureByteStride;
}
