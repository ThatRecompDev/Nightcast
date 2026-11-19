// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// ID3D11DeviceContext (d3d11.h), up to CopySubresourceRegion: the slots before it are declared in the header's order
/// (ID3D11DeviceChild's four first) so the vtable lines up; only Map, Unmap and CopySubresourceRegion are called.
/// </summary>
[GeneratedComInterface]
[Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da")]
internal partial interface ID3D11DeviceContext
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

    void Begin();

    void End();

    void GetData();

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

    void Dispatch();

    void DispatchIndirect();

    void RSSetState();

    void RSSetViewports();

    void RSSetScissorRects();

    [PreserveSig]
    void CopySubresourceRegion(nint destination, uint destinationSubresource, uint x, uint y, uint z, nint source, uint sourceSubresource, nint box);
}
