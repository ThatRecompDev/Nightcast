// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// ID3D11DeviceContext (d3d11.h), up to CSSetConstantBuffers: the slots are declared in the header's order
/// (ID3D11DeviceChild's four first) so the vtable lines up. The decoder calls Map, Unmap and CopySubresourceRegion;
/// the frame generation (Phantom.Relay.Lsfg) calls the compute-stage setters, Dispatch, the copies and the queries.
/// Unused slots are declared without parameters.
/// </summary>
[GeneratedComInterface]
[Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da")]
internal unsafe partial interface ID3D11DeviceContext
{
    void GetDevice();

    void GetPrivateData();

    void SetPrivateData();

    void SetPrivateDataInterface();

    void VSSetConstantBuffers();

    void PSSetShaderResources();

    void PSSetShader();

    void PSSetSamplers();

    void VSSetShader();

    void DrawIndexed();

    void Draw();

    [PreserveSig]
    int Map(nint resource, uint subresource, uint mapType, uint flags, out MappedSubresource mapped);

    [PreserveSig]
    void Unmap(nint resource, uint subresource);

    void PSSetConstantBuffers();

    void IASetInputLayout();

    void IASetVertexBuffers();

    void IASetIndexBuffer();

    void DrawIndexedInstanced();

    void DrawInstanced();

    void GSSetConstantBuffers();

    void GSSetShader();

    void IASetPrimitiveTopology();

    void VSSetShaderResources();

    void VSSetSamplers();

    [PreserveSig]
    void Begin(nint asynchronous);

    [PreserveSig]
    void End(nint asynchronous);

    [PreserveSig]
    int GetData(nint asynchronous, void* data, uint size, uint flags);

    void SetPredication();

    void GSSetShaderResources();

    void GSSetSamplers();

    void OMSetRenderTargets();

    void OMSetRenderTargetsAndUnorderedAccessViews();

    void OMSetBlendState();

    void OMSetDepthStencilState();

    void SOSetTargets();

    void DrawAuto();

    void DrawIndexedInstancedIndirect();

    void DrawInstancedIndirect();

    [PreserveSig]
    void Dispatch(uint groupsX, uint groupsY, uint groupsZ);

    void DispatchIndirect();

    void RSSetState();

    void RSSetViewports();

    void RSSetScissorRects();

    [PreserveSig]
    void CopySubresourceRegion(nint destination, uint destinationSubresource, uint x, uint y, uint z, nint source, uint sourceSubresource, nint box);

    [PreserveSig]
    void CopyResource(nint destination, nint source);

    [PreserveSig]
    void UpdateSubresource(nint destination, uint subresource, nint box, void* data, uint rowPitch, uint depthPitch);

    void CopyStructureCount();

    void ClearRenderTargetView();

    void ClearUnorderedAccessViewUint();

    [PreserveSig]
    void ClearUnorderedAccessViewFloat(nint view, float* values);

    void ClearDepthStencilView();

    void GenerateMips();

    void SetResourceMinLOD();

    void GetResourceMinLOD();

    void ResolveSubresource();

    void ExecuteCommandList();

    void HSSetShaderResources();

    void HSSetShader();

    void HSSetSamplers();

    void HSSetConstantBuffers();

    void DSSetShaderResources();

    void DSSetShader();

    void DSSetSamplers();

    void DSSetConstantBuffers();

    [PreserveSig]
    void CSSetShaderResources(uint startSlot, uint count, nint* views);

    [PreserveSig]
    void CSSetUnorderedAccessViews(uint startSlot, uint count, nint* views, uint* initialCounts);

    [PreserveSig]
    void CSSetShader(nint shader, nint classInstances, uint classInstanceCount);

    [PreserveSig]
    void CSSetSamplers(uint startSlot, uint count, nint* samplers);

    [PreserveSig]
    void CSSetConstantBuffers(uint startSlot, uint count, nint* buffers);
}
