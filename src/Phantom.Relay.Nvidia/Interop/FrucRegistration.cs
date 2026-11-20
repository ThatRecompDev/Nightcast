// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// NvOFFRUC_REGISTER_RESOURCE_PARAM (NvOFFRUC.h): the textures FRUC may be given (inputs and output, made on the
/// device FRUC was created with), the shared ID3D11Fence both sides synchronise on (null: keyed mutexes), and their
/// count; 96 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FrucRegistration
{
    public FrucResources Resources;
    public nint Fence;
    public uint Count;
}
