// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// A few NV12 textures on the decoder's D3D11 device that another device on the same adapter (the renderer's) opens by
/// their NT handles, and a shared fence that says when each was written: the decoder's pictures handed over on the GPU,
/// never read back. A decoder's own surfaces cannot be shared, so each picture is copied into a slot on the GPU
/// (<see cref="Copy"/>), and <see cref="Signal"/> then marks it written. Before a slot is written again the decoder's
/// queue waits, on the GPU, for the renderer's fence to reach the value its last read of the slot ends at
/// (<see cref="WaitForReader"/>), so the CPU never blocks. Used from the decoder's thread; the handles are read by the
/// renderer's. Allocates nothing per picture.
/// </summary>
/// <remarks>
/// The textures are bound as shader resources (the renderer samples their two planes) and, where the GPU allows it, as
/// render targets too, which a video processor needs to write a slot (<see cref="IsWritableByVideoProcessor"/>).
/// Their size is the decoder's surface size, which may be padded past the picture's (1080 coded as 1088).
/// </remarks>
public sealed unsafe class SharedSurfaceRing : IDisposable
{
    private readonly ComWrappers _wrappers = new StrategyBasedComWrappers();
    private readonly ID3D11DeviceContext _contextApi;
    private readonly nint[] _textures;
    private readonly nint[] _handles;
    private nint _device;
    private nint _device5;
    private nint _context;
    private nint _context4;
    private nint _fence;
    private nint _fenceHandle;
    private nint _reader;
    private nint _readerHandle;
    private ulong _written;

    private SharedSurfaceRing(nint device, int count, int width, int height)
    {
        _device = device;
        _ = Marshal.AddRef(device);
        _textures = new nint[count];
        _handles = new nint[count];
        Width = width;
        Height = height;

        var deviceApi = ComObjects.Wrap<ID3D11Device>(_wrappers, device);
        deviceApi.GetImmediateContext(out _context);
        ComObjects.Free(deviceApi);
        _contextApi = ComObjects.Wrap<ID3D11DeviceContext>(_wrappers, _context);
    }

    /// <summary>The slots.</summary>
    public int Count => _textures.Length;

    /// <summary>Each slot's width in texels: the decoder's surface width.</summary>
    public int Width { get; }

    /// <summary>Each slot's height in texels: the decoder's surface height.</summary>
    public int Height { get; }

    /// <summary>Whether the slots are also render targets, so a video processor can write them.</summary>
    public bool IsWritableByVideoProcessor { get; private set; }

    /// <summary>The NT handle of the fence <see cref="Signal"/> sets; the ring keeps it open.</summary>
    public nint FenceHandle => _fenceHandle;

    /// <summary>The value the fence was last set to: the newest written slot's.</summary>
    public ulong Written => _written;

    /// <summary>
    /// A ring of <paramref name="count"/> NV12 slots of <paramref name="width"/> x <paramref name="height"/> on
    /// <paramref name="device"/> (an ID3D11Device the caller keeps; the ring holds its own reference), with its shared
    /// fence; null and why when the device cannot share (no D3D11.4 fences, or NV12 textures it will not share).
    /// </summary>
    public static SharedSurfaceRing? TryCreate(nint device, int count, int width, int height, out string? problem)
    {
        var ring = new SharedSurfaceRing(device, count, width, height);
        problem = ring.Prepare();

        if (problem is not null)
        {
            ring.Dispose();

            return null;
        }

        return ring;
    }

    /// <summary>The NT handle of slot <paramref name="slot"/>'s texture; the ring keeps it open.</summary>
    public nint Handle(int slot)
    {
        return _handles[slot];
    }

    /// <summary>Slot <paramref name="slot"/>'s ID3D11Texture2D, borrowed.</summary>
    public nint Texture(int slot)
    {
        return _textures[slot];
    }

    /// <summary>
    /// Holds the decoder's later work on the GPU until the reader's fence (<paramref name="readerFence"/>, its NT handle,
    /// opened once) reaches <paramref name="value"/>: the end of the reader's last access to a slot. A handle of 0 waits
    /// for nothing. False when the fence cannot be opened or waited on.
    /// </summary>
    [HotPath]
    public bool WaitForReader(nint readerFence, ulong value)
    {
        if (readerFence == 0)
        {
            return true;
        }

        if (readerFence != _readerHandle && !OpenReader(readerFence))
        {
            return false;
        }

        return !ComObjects.Failed(SharingCalls.Wait(_context4, _reader, value));
    }

    /// <summary>Queues the copy of a decoded surface (slice <paramref name="slice"/> of <paramref name="texture"/>) into a slot.</summary>
    [HotPath]
    public void Copy(int slot, nint texture, uint slice)
    {
        _contextApi.CopySubresourceRegion(_textures[slot], 0, 0, 0, 0, texture, slice, 0);
    }

    /// <summary>Sets the fence to the next value once the work queued so far is done, sends it to the GPU, and answers the value.</summary>
    [HotPath]
    public ulong Signal()
    {
        _written++;
        _ = SharingCalls.Signal(_context4, _fence, _written);
        SharingCalls.Flush(_context);

        return _written;
    }

    public void Dispose()
    {
        for (var slot = 0; slot < _textures.Length; slot++)
        {
            Close(ref _handles[slot]);
            ComObjects.Release(ref _textures[slot]);
        }

        Close(ref _fenceHandle);
        ComObjects.Release(ref _fence);
        ComObjects.Release(ref _reader);
        ComObjects.Release(ref _context4);
        ComObjects.Release(ref _device5);

        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _context);
        ComObjects.Release(ref _device);
    }

    private static void Close(ref nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        _ = NativeMethods.CloseHandle(handle);
        handle = 0;
    }

    // The D3D11.4 interfaces, the fence and its handle, then the slots and theirs.
    private string? Prepare()
    {
        _device5 = SharingCalls.Query(_device, in SharingCalls.Device5);
        _context4 = SharingCalls.Query(_context, in SharingCalls.DeviceContext4);

        if (_device5 == 0 || _context4 == 0)
        {
            return "the decoder's D3D11 device has no shared fences (ID3D11Device5, ID3D11DeviceContext4: Windows 10 1703 or later)";
        }

        var result = SharingCalls.CreateFence(_device5, out _fence);

        if (!ComObjects.Failed(result))
        {
            result = SharingCalls.CreateFenceHandle(_fence, out _fenceHandle);
        }

        if (ComObjects.Failed(result))
        {
            return $"the decoder's D3D11 device refused a shared fence (0x{result:X8})";
        }

        return PrepareSlots();
    }

    private string? PrepareSlots()
    {
        IsWritableByVideoProcessor = true;

        for (var slot = 0; slot < _textures.Length; slot++)
        {
            var problem = PrepareSlot(slot);

            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    // One slot: shareable NV12, a render target where the GPU allows (the first slot decides for all), and its handle.
    private string? PrepareSlot(int slot)
    {
        var deviceApi = ComObjects.Wrap<ID3D11Device>(_wrappers, _device);
        var result = deviceApi.CreateTexture2D(Description(IsWritableByVideoProcessor), 0, out _textures[slot]);

        if (ComObjects.Failed(result) && slot == 0)
        {
            IsWritableByVideoProcessor = false;
            result = deviceApi.CreateTexture2D(Description(false), 0, out _textures[slot]);
        }

        ComObjects.Free(deviceApi);

        if (ComObjects.Failed(result))
        {
            return $"the decoder's D3D11 device refused a shared NV12 texture of {Width}x{Height} (0x{result:X8})";
        }

        var resource = SharingCalls.Query(_textures[slot], in SharingCalls.DxgiResource1);

        if (resource == 0)
        {
            return "the shared NV12 texture has no IDXGIResource1";
        }

        result = SharingCalls.CreateSharedHandle(resource, out _handles[slot]);
        ComObjects.Release(ref resource);

        if (ComObjects.Failed(result))
        {
            return $"the shared NV12 texture gave no NT handle (0x{result:X8})";
        }

        return null;
    }

    private Texture2DDescription Description(bool renderTarget)
    {
        var bind = Direct3D11Values.BindShaderResource;

        if (renderTarget)
        {
            bind |= Direct3D11Values.BindRenderTarget;
        }

        return new Texture2DDescription
        {
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = MediaFoundationValues.Nv12Format,
            SampleCount = 1,
            Usage = Direct3D11Values.DefaultUsage,
            BindFlags = bind,
            MiscFlags = Direct3D11Values.MiscShared | Direct3D11Values.MiscSharedNtHandle,
        };
    }

    // The reader's fence, opened once per handle it names (a renderer keeps one fence for its life).
    private bool OpenReader(nint handle)
    {
        ComObjects.Release(ref _reader);
        _readerHandle = 0;

        if (ComObjects.Failed(SharingCalls.OpenSharedFence(_device5, handle, out _reader)))
        {
            return false;
        }

        _readerHandle = handle;

        return true;
    }
}
