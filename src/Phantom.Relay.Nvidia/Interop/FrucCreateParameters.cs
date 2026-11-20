// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// NvOFFRUC_CREATE_PARAM (NvOFFRUC.h): the frame size, the client's ID3D11Device, the resource type (DirectX 11), the
/// surface format (NV12 or ARGB) and the CUDA resource type (unused with DirectX), then 32 reserved words; 160 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FrucCreateParameters
{
    public uint Width;
    public uint Height;
    public nint Device;
    public int ResourceType;
    public int SurfaceFormat;
    public int CudaResourceType;
    public fixed uint Reserved[FrucValues.ReservedWords];
}
