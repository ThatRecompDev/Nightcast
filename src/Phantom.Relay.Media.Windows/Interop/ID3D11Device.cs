// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// ID3D11Device (d3d11.h), up to GetImmediateContext: every slot in the header's order so the vtable lines up. The
/// decoder makes its staging texture through it; the frame generation (Phantom.Relay.Lsfg) makes its compute
/// shaders, views, samplers, constant buffers and timing queries. Unused slots are declared without parameters.
/// </summary>
[GeneratedComInterface]
[Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
internal unsafe partial interface ID3D11Device
{
    [PreserveSig]
    int CreateBuffer(in BufferDescription description, SubresourceData* initialData, out nint buffer);

    void CreateTexture1D();

    [PreserveSig]
    int CreateTexture2D(in Texture2DDescription description, nint initialData, out nint texture);

    void CreateTexture3D();

    [PreserveSig]
    int CreateShaderResourceView(nint resource, in ShaderResourceViewDescription description, out nint view);

    [PreserveSig]
    int CreateUnorderedAccessView(nint resource, in UnorderedAccessViewDescription description, out nint view);

    void CreateRenderTargetView();

    void CreateDepthStencilView();

    void CreateInputLayout();

    void CreateVertexShader();

    void CreateGeometryShader();

    void CreateGeometryShaderWithStreamOutput();

    void CreatePixelShader();

    void CreateHullShader();

    void CreateDomainShader();

    [PreserveSig]
    int CreateComputeShader(byte* bytecode, nuint length, nint classLinkage, out nint shader);

    void CreateClassLinkage();

    void CreateBlendState();

    void CreateDepthStencilState();

    void CreateRasterizerState();

    [PreserveSig]
    int CreateSamplerState(in SamplerDescription description, out nint sampler);

    [PreserveSig]
    int CreateQuery(in QueryDescription description, out nint query);

    void CreatePredicate();

    void CreateCounter();

    void CreateDeferredContext();

    void OpenSharedResource();

    void CheckFormatSupport();

    void CheckMultisampleQualityLevels();

    void CheckCounterInfo();

    void CheckCounter();

    void CheckFeatureSupport();

    void GetPrivateData();

    void SetPrivateData();

    void SetPrivateDataInterface();

    void GetFeatureLevel();

    void GetCreationFlags();

    void GetDeviceRemovedReason();

    [PreserveSig]
    void GetImmediateContext(out nint context);
}
