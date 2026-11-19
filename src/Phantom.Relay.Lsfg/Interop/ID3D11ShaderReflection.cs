// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>
/// ID3D11ShaderReflection (d3d11shader.h), up to GetThreadGroupSize, every slot in the header's order: what Windows
/// reports about a compiled shader. Its constant buffers come back as raw pointers to objects that are not COM
/// objects (no IUnknown), read through <see cref="ShaderReflectionMembers"/>.
/// </summary>
[GeneratedComInterface]
[Guid("8d536ca1-0cca-4956-a837-786963755584")]
internal unsafe partial interface ID3D11ShaderReflection
{
    [PreserveSig]
    int GetDesc(ShaderDescription* description);

    [PreserveSig]
    nint GetConstantBufferByIndex(uint index);

    void GetConstantBufferByName();

    [PreserveSig]
    int GetResourceBindingDesc(uint index, ShaderInputBindDescription* description);

    void GetInputParameterDesc();

    void GetOutputParameterDesc();

    void GetPatchConstantParameterDesc();

    void GetVariableByName();

    void GetResourceBindingDescByName();

    void GetMovInstructionCount();

    void GetMovcInstructionCount();

    void GetConversionInstructionCount();

    void GetBitwiseInstructionCount();

    void GetGSInputPrimitive();

    void IsSampleFrequencyShader();

    void GetNumInterfaceSlots();

    void GetMinFeatureLevel();

    [PreserveSig]
    uint GetThreadGroupSize(uint* x, uint* y, uint* z);
}
