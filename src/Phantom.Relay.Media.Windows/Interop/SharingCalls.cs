// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// The calls of DXGI 1.4 and D3D11.4 that pick an adapter by its LUID and share textures and fences with another device
/// on it, made through each interface's table of functions: the slots of the Windows SDK's dxgi1_2.h, dxgi1_4.h,
/// d3d11.h, d3d11_3.h and d3d11_4.h, counted from IUnknown's three, the same Phantom.Engine's shared-texture live test
/// checks against its own device. Only the few calls the decoder's GPU path makes, so the large interfaces they sit at
/// the end of are not declared slot by slot. Each returns the HRESULT; a reference it hands out is the caller's.
/// </summary>
internal static unsafe class SharingCalls
{
    /// <summary>IDXGIFactory4::EnumAdapterByLuid.</summary>
    private const int EnumAdapterByLuidSlot = 26;

    /// <summary>IDXGIResource1::CreateSharedHandle.</summary>
    private const int ResourceCreateSharedHandleSlot = 13;

    /// <summary>ID3D11Fence::CreateSharedHandle.</summary>
    private const int FenceCreateSharedHandleSlot = 7;

    /// <summary>ID3D11Fence::GetCompletedValue.</summary>
    private const int FenceCompletedValueSlot = 8;

    /// <summary>ID3D11Device5::OpenSharedFence.</summary>
    private const int OpenSharedFenceSlot = 67;

    /// <summary>ID3D11Device5::CreateFence.</summary>
    private const int CreateFenceSlot = 68;

    /// <summary>ID3D11DeviceContext::Flush.</summary>
    private const int FlushSlot = 111;

    /// <summary>ID3D11DeviceContext4::Signal.</summary>
    private const int SignalSlot = 147;

    /// <summary>ID3D11DeviceContext4::Wait.</summary>
    private const int WaitSlot = 148;

    /// <summary>DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE (dxgi1_2.h).</summary>
    private const uint SharedResourceReadWrite = 0x80000001;

    /// <summary>GENERIC_ALL (winnt.h): what a fence's shared handle grants.</summary>
    private const uint GenericAll = 0x10000000;

    /// <summary>D3D11_FENCE_FLAG_SHARED (d3d11_3.h).</summary>
    private const uint FenceShared = 0x2;

    /// <summary>IID_IDXGIFactory4.</summary>
    public static readonly Guid DxgiFactory4 = new("1bc6ea02-ef36-464f-bf0c-21ca39e5168a");

    /// <summary>IID_IDXGIAdapter.</summary>
    public static readonly Guid DxgiAdapter = new("2411e7e1-12ac-4ccf-bd14-9798e8534dc0");

    /// <summary>IID_IDXGIResource1.</summary>
    public static readonly Guid DxgiResource1 = new("30961379-4609-4a41-998e-54fe567ee0c1");

    /// <summary>IID_ID3D11Device5.</summary>
    public static readonly Guid Device5 = new("8ffde202-a0e7-45df-9e01-e837801b5ea0");

    /// <summary>IID_ID3D11DeviceContext4.</summary>
    public static readonly Guid DeviceContext4 = new("917600da-f58c-4c33-98d8-3e15b390fa24");

    /// <summary>IID_ID3D11Fence.</summary>
    public static readonly Guid Fence = new("affde9d1-1df7-4bb7-8a34-0f46251dab80");

    /// <summary>The adapter of <paramref name="luid"/> (DXGI's LUID, its high part in the upper 32 bits).</summary>
    public static int EnumAdapterByLuid(nint factory4, long luid, out nint adapter)
    {
        nint found = 0;
        var iid = DxgiAdapter;
        var call = (delegate* unmanaged[Stdcall]<nint, long, Guid*, nint*, int>)Slot(factory4, EnumAdapterByLuidSlot);
        var result = call(factory4, luid, &iid, &found);
        adapter = found;

        return result;
    }

    /// <summary>An NT handle to a texture made with D3D11_RESOURCE_MISC_SHARED_NTHANDLE, readable and writable.</summary>
    public static int CreateSharedHandle(nint resource1, out nint handle)
    {
        nint made = 0;
        var call = (delegate* unmanaged[Stdcall]<nint, void*, uint, char*, nint*, int>)Slot(resource1, ResourceCreateSharedHandleSlot);
        var result = call(resource1, null, SharedResourceReadWrite, null, &made);
        handle = made;

        return result;
    }

    /// <summary>A shared fence of the device, starting at 0.</summary>
    public static int CreateFence(nint device5, out nint fence)
    {
        nint made = 0;
        var iid = Fence;
        var call = (delegate* unmanaged[Stdcall]<nint, ulong, uint, Guid*, nint*, int>)Slot(device5, CreateFenceSlot);
        var result = call(device5, 0, FenceShared, &iid, &made);
        fence = made;

        return result;
    }

    /// <summary>An NT handle to a shared fence.</summary>
    public static int CreateFenceHandle(nint fence, out nint handle)
    {
        nint made = 0;
        var call = (delegate* unmanaged[Stdcall]<nint, void*, uint, char*, nint*, int>)Slot(fence, FenceCreateSharedHandleSlot);
        var result = call(fence, null, GenericAll, null, &made);
        handle = made;

        return result;
    }

    /// <summary>A fence another device shares, opened on this one.</summary>
    public static int OpenSharedFence(nint device5, nint handle, out nint fence)
    {
        nint opened = 0;
        var iid = Fence;
        var call = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)Slot(device5, OpenSharedFenceSlot);
        var result = call(device5, handle, &iid, &opened);
        fence = opened;

        return result;
    }

    /// <summary>The value a fence has reached.</summary>
    public static ulong CompletedValue(nint fence)
    {
        var call = (delegate* unmanaged[Stdcall]<nint, ulong>)Slot(fence, FenceCompletedValueSlot);

        return call(fence);
    }

    /// <summary>Has the GPU set <paramref name="fence"/> to <paramref name="value"/> once the context's work so far is done.</summary>
    public static int Signal(nint context4, nint fence, ulong value)
    {
        var call = (delegate* unmanaged[Stdcall]<nint, nint, ulong, int>)Slot(context4, SignalSlot);

        return call(context4, fence, value);
    }

    /// <summary>Has the GPU hold the context's later work until <paramref name="fence"/> reaches <paramref name="value"/>.</summary>
    public static int Wait(nint context4, nint fence, ulong value)
    {
        var call = (delegate* unmanaged[Stdcall]<nint, nint, ulong, int>)Slot(context4, WaitSlot);

        return call(context4, fence, value);
    }

    /// <summary>Sends the context's queued work to the GPU.</summary>
    public static void Flush(nint context)
    {
        var call = (delegate* unmanaged[Stdcall]<nint, void>)Slot(context, FlushSlot);
        call(context);
    }

    /// <summary>The interface <paramref name="iid"/> of an object; a reference of its own, 0 when it has none.</summary>
    public static nint Query(nint unknown, in Guid iid)
    {
        if (Marshal.QueryInterface(unknown, in iid, out var result) < 0)
        {
            return 0;
        }

        return result;
    }

    private static void* Slot(nint unknown, int slot)
    {
        return (*(void***)unknown)[slot];
    }
}
