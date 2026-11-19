// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>The numbers of D3D's shader reflection the loader reads, as d3dcommon.h and d3d11shader.h name them.</summary>
internal static class ShaderReflectionValues
{
    /// <summary>D3D_SIT_CBUFFER.</summary>
    public const uint ConstantBufferInput = 0;

    /// <summary>D3D_SIT_TEXTURE.</summary>
    public const uint TextureInput = 2;

    /// <summary>D3D_SIT_SAMPLER.</summary>
    public const uint SamplerInput = 3;

    /// <summary>D3D_SIT_UAV_RWTYPED.</summary>
    public const uint TypedUavInput = 4;

    /// <summary>D3D_SIF_TEXTURE_COMPONENTS (d3dcommon.h): two bits holding the component count less one.</summary>
    public const uint ComponentMask = 0xC;

    /// <summary>Where D3D_SIF_TEXTURE_COMPONENTS starts in the bind flags.</summary>
    public const int ComponentShift = 2;

    /// <summary>D3D_SVF_USED (d3dcommon.h): the shader reads the variable.</summary>
    public const uint VariableUsed = 0x2;

    /// <summary>The compute shader program type in a shader version's high word (D3D11_SHVER_COMPUTE_SHADER).</summary>
    public const uint ComputeProgram = 5;

    /// <summary>Where the program type starts in a shader version (D3D11_SHVER_GET_TYPE).</summary>
    public const int ProgramTypeShift = 16;

    /// <summary>"DXBC", the first four bytes of a compiled D3D10 to D3D11 shader container, little-endian.</summary>
    public const uint ContainerMagic = 0x43425844;

    /// <summary>IID_ID3D11ShaderReflection (d3d11shader.h).</summary>
    public static readonly Guid ReflectionInterface = new("8d536ca1-0cca-4956-a837-786963755584");
}
