// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>D3D11_SHADER_BUFFER_DESC (d3d11shader.h): a constant buffer's name, kind, variable count and size.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShaderBufferDescription
{
    public nint Name;
    public uint Type;
    public uint Variables;
    public uint Size;
    public uint Flags;
}
