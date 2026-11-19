// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Tests.Pictures;

/// <summary>The LUID of the system's first DXGI adapter (the one a renderer opens by default), through DXGI's vtables.</summary>
internal static unsafe class FirstAdapter
{
    // IDXGIFactory1::EnumAdapters1 and IDXGIAdapter1::GetDesc1, by their vtable slots (dxgi.h, IUnknown first).
    private const int EnumAdapters1Slot = 12;
    private const int GetDesc1Slot = 10;
    private const int ReleaseSlot = 2;

    // DXGI_ADAPTER_DESC1: the LUID follows the 128-character description, four ids and three sizes.
    private const int DescriptionBytes = 320;
    private const int LuidOffset = 296;

    private static readonly Guid FactoryInterface = new("770aae78-f26f-4dba-a829-253c83d1b387");

    public static long Luid()
    {
        var factoryId = FactoryInterface;
        Assert.True(CreateDXGIFactory1(&factoryId, out var factory) >= 0, "no DXGI factory");
        nint adapter;
        var enumerate = (delegate* unmanaged<nint, uint, nint*, int>)(*(nint**)factory)[EnumAdapters1Slot];
        Assert.True(enumerate(factory, 0, &adapter) >= 0, "no DXGI adapter");

        var description = stackalloc byte[DescriptionBytes];
        var describe = (delegate* unmanaged<nint, byte*, int>)(*(nint**)adapter)[GetDesc1Slot];
        Assert.True(describe(adapter, description) >= 0, "the adapter gave no description");
        var luid = *(long*)(description + LuidOffset);

        Release(adapter);
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
