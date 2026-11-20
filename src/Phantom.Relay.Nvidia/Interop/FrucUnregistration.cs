// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>NvOFFRUC_UNREGISTER_RESOURCE_PARAM (NvOFFRUC.h): the textures registered before, and their count; 88 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FrucUnregistration
{
    public FrucResources Resources;
    public uint Count;
}
