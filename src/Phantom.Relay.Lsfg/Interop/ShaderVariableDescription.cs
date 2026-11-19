// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>D3D11_SHADER_VARIABLE_DESC (d3d11shader.h): a constant's name, offset, size and flags.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShaderVariableDescription
{
    public nint Name;
    public uint StartOffset;
    public uint Size;
    public uint Flags;
    public nint DefaultValue;
    public uint StartTexture;
    public uint TextureSize;
    public uint StartSampler;
    public uint SamplerSize;
}
