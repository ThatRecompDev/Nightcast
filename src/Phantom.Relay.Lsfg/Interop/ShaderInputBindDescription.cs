// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>D3D11_SHADER_INPUT_BIND_DESC (d3d11shader.h): one resource a shader binds, and at which slot.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShaderInputBindDescription
{
    public nint Name;
    public uint Type;
    public uint BindPoint;
    public uint BindCount;
    public uint Flags;
    public uint ReturnType;
    public uint Dimension;
    public uint SampleCount;
}
