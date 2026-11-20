// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Tests.Pictures;

/// <summary>
/// The LUID of the system's first DXGI adapter (the one a renderer opens by default), or of its first NVIDIA one, through
/// DXGI's vtables.
/// </summary>
internal static unsafe class FirstAdapter
{
    // IDXGIFactory1::EnumAdapters1 and IDXGIAdapter1::GetDesc1, by their vtable slots (dxgi.h, IUnknown first).
    private const int EnumAdapters1Slot = 12;
    private const int GetDesc1Slot = 10;
    private const int ReleaseSlot = 2;

    // DXGI_ADAPTER_DESC1: the vendor id follows the 128-character description; the LUID follows four ids and three sizes.
    private const int DescriptionBytes = 320;
    private const int VendorOffset = 256;
    private const int LuidOffset = 296;
    private const uint NvidiaVendor = 0x10DE;

    private static readonly Guid FactoryInterface = new("770aae78-f26f-4dba-a829-253c83d1b387");

    public static long Luid()
    {
        return Find(vendor => true);
    }

    /// <summary>The first NVIDIA adapter's LUID.</summary>
    public static long Nvidia()
    {
        return Find(vendor => vendor == NvidiaVendor);
    }

    private static long Find(Func<uint, bool> wanted)
    {
        var factoryId = FactoryInterface;
        Assert.True(CreateDXGIFactory1(&factoryId, out var factory) >= 0, "no DXGI factory");
        var enumerate = (delegate* unmanaged<nint, uint, nint*, int>)(*(nint**)factory)[EnumAdapters1Slot];
        var description = stackalloc byte[DescriptionBytes];
        long luid = 0;

        for (uint index = 0; luid == 0; index++)
        {
            nint adapter;
            Assert.True(enumerate(factory, index, &adapter) >= 0, "no such DXGI adapter");
            var describe = (delegate* unmanaged<nint, byte*, int>)(*(nint**)adapter)[GetDesc1Slot];
            Assert.True(describe(adapter, description) >= 0, "the adapter gave no description");

            if (wanted(*(uint*)(description + VendorOffset)))
            {
                luid = *(long*)(description + LuidOffset);
            }

            Release(adapter);
        }

        Release(factory);

        return luid;
    }

    private static void Release(nint instance)
    {
        var release = (delegate* unmanaged<nint, uint>)(*(nint**)instance)[ReleaseSlot];
        _ = release(instance);
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(Guid* id, out nint factory);
}
