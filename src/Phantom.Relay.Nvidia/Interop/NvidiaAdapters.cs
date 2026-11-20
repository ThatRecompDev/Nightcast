// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// Which GPU a D3D11 device sits on, and the first NVIDIA one: FRUC runs only on an NVIDIA GPU's optical flow
/// accelerator, and a PC with an integrated GPU beside a discrete one has two. Through DXGI's tables of functions
/// (dxgi.h's slots, counted from IUnknown's three), as <see cref="SharingCalls"/> does for its few calls.
/// </summary>
internal static unsafe class NvidiaAdapters
{
    /// <summary>PCI vendor id of NVIDIA.</summary>
    public const uint NvidiaVendor = 0x10DE;

    /// <summary>IDXGIDevice::GetAdapter.</summary>
    private const int GetAdapterSlot = 7;

    /// <summary>IDXGIAdapter::GetDesc.</summary>
    private const int GetDescriptionSlot = 8;

    /// <summary>IDXGIFactory1::EnumAdapters1.</summary>
    private const int EnumAdapters1Slot = 12;

    /// <summary>DXGI_ADAPTER_DESC(1): WCHAR Description[128] and then VendorId.</summary>
    private const int VendorOffset = 256;

    /// <summary>sizeof(DXGI_ADAPTER_DESC1), the larger of the two (its Flags after the LUID).</summary>
    private const int DescriptionSize = 312;

    /// <summary>DXGI_ERROR_NOT_FOUND: no adapter at that index.</summary>
    private const int NotFound = unchecked((int)0x887A0002);

    /// <summary>IID_IDXGIDevice.</summary>
    private static readonly Guid DxgiDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    /// <summary>IID_IDXGIFactory1.</summary>
    private static readonly Guid DxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    /// <summary>The PCI vendor id of the GPU <paramref name="device"/> (an ID3D11Device) runs on; 0 when DXGI does not say.</summary>
    public static uint VendorOf(nint device)
    {
        var dxgi = SharingCalls.Query(device, in DxgiDevice);

        if (dxgi == 0)
        {
            return 0;
        }

        nint adapter = 0;
        var getAdapter = (delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(dxgi, GetAdapterSlot);
        var result = getAdapter(dxgi, &adapter);
        ComObjects.Release(ref dxgi);

        if (ComObjects.Failed(result))
        {
            return 0;
        }

        var vendor = Vendor(adapter);
        ComObjects.Release(ref adapter);

        return vendor;
    }

    /// <summary>The first NVIDIA adapter (a reference the caller releases), or 0.</summary>
    public static nint FindNvidia()
    {
        if (ComObjects.Failed(NativeMethods.CreateDXGIFactory1(in DxgiFactory1, out var factory)))
        {
            return 0;
        }

        var enumerate = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(factory, EnumAdapters1Slot);
        nint found = 0;

        for (uint index = 0; found == 0; index++)
        {
            nint adapter = 0;

            if (enumerate(factory, index, &adapter) == NotFound)
            {
                break;
            }

            if (Vendor(adapter) == NvidiaVendor)
            {
                found = adapter;
            }
            else
            {
                ComObjects.Release(ref adapter);
            }
        }

        ComObjects.Release(ref factory);

        return found;
    }

    private static uint Vendor(nint adapter)
    {
        var description = stackalloc byte[DescriptionSize];
        var getDescription = (delegate* unmanaged[Stdcall]<nint, byte*, int>)Slot(adapter, GetDescriptionSlot);

        if (ComObjects.Failed(getDescription(adapter, description)))
        {
            return 0;
        }

        return *(uint*)(description + VendorOffset);
    }

    private static void* Slot(nint unknown, int slot)
    {
        return (*(void***)unknown)[slot];
    }
}
