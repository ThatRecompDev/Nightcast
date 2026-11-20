// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// The D3D11 device the decoder runs DXVA on, shared with it through an IMFDXGIDeviceManager, and the one staging
/// texture its pictures are read back through: a decoded surface is copied into it on the GPU
/// (<see cref="TryCopy"/>), then mapped and split into a <see cref="VideoFrame"/>'s planes (<see cref="TryRead"/>).
/// The staging texture is made again only when the decoder's surfaces change size.
/// </summary>
internal sealed unsafe class D3D11VideoDevice : IDisposable
{
    private readonly ComWrappers _wrappers;
    private readonly ID3D11Device _deviceApi;
    private readonly ID3D11DeviceContext _contextApi;
    private nint _device;
    private nint _context;
    private nint _manager;
    private nint _staging;
    private nint _stagingSource;
    private Texture2DDescription _stagingDescription;

    private D3D11VideoDevice(ComWrappers wrappers, nint device, nint context, nint manager)
    {
        _wrappers = wrappers;
        _device = device;
        _context = context;
        _manager = manager;
        _deviceApi = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        _contextApi = ComObjects.Wrap<ID3D11DeviceContext>(wrappers, context);
    }

    /// <summary>The device manager to hand the decoder (MFT_MESSAGE_SET_D3D_MANAGER).</summary>
    public nint Manager => _manager;

    /// <summary>The ID3D11Device, borrowed: the device's own reference.</summary>
    public nint Device => _device;

    /// <summary>
    /// A hardware device with video support, made thread-safe and put in a device manager, on the adapter of
    /// <paramref name="adapterLuid"/> (0: the system's default adapter); null and why when not.
    /// </summary>
    public static D3D11VideoDevice? TryCreate(ComWrappers wrappers, long adapterLuid, out string? problem)
    {
        if (!TryCreateDevice(adapterLuid, out var device, out var context, out problem))
        {
            return null;
        }

        if (!TryProtect(wrappers, device, out problem) || !TryCreateManager(wrappers, device, out var manager, out problem))
        {
            ComObjects.Release(ref context);
            ComObjects.Release(ref device);

            return null;
        }

        return new D3D11VideoDevice(wrappers, device, context, manager);
    }

    /// <summary>Forgets which surface the staging texture was sized for, after the decoder's output changed.</summary>
    public void Forget()
    {
        _stagingSource = 0;
    }

    /// <summary>Queues the copy of a decoded surface (one slice of the decoder's texture array) into the staging texture.</summary>
    [HotPath]
    public bool TryCopy(nint texture, uint subresource)
    {
        if (texture != _stagingSource && !TryPrepareStaging(texture))
        {
            return false;
        }

        _contextApi.CopySubresourceRegion(_staging, 0, 0, 0, 0, texture, subresource, 0);

        return true;
    }

    /// <summary>Maps the staging texture (waiting for the decode and the copy) and splits it into the frame's planes.</summary>
    [HotPath]
    public bool TryRead(int width, int height, VideoFrame target)
    {
        var result = _contextApi.Map(_staging, 0, MediaFoundationValues.MapRead, 0, out var mapped);

        if (ComObjects.Failed(result))
        {
            return false;
        }

        try
        {
            // NV12 in a mapped texture: the UV plane follows the texture's own rows, not the picture's.
            var pitch = (int)mapped.RowPitch;
            var rows = (int)_stagingDescription.Height;
            var lumaBytes = pitch * rows;
            var luma = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>((void*)mapped.Data), lumaBytes);
            var chroma = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>((byte*)mapped.Data + lumaBytes), pitch * VideoFrame.ChromaSide(rows, 1));
            Nv12Planes.CopyToFrame(luma, chroma, pitch, width, height, target);
        }
        finally
        {
            _contextApi.Unmap(_staging, 0);
        }

        return true;
    }

    public void Dispose()
    {
        ComObjects.Free(_deviceApi);
        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _staging);
        ComObjects.Release(ref _manager);
        ComObjects.Release(ref _context);
        ComObjects.Release(ref _device);
    }

    // The device on the given adapter (with the driver it names), or on the default hardware adapter for LUID 0.
    private static bool TryCreateDevice(long adapterLuid, out nint device, out nint context, out string? problem)
    {
        device = 0;
        context = 0;
        problem = null;
        nint adapter = 0;
        var driver = MediaFoundationValues.HardwareDriver;

        if (adapterLuid != 0)
        {
            adapter = DxgiAdapters.Find(adapterLuid, out problem);
            driver = Direct3D11Values.UnknownDriver;
        }

        if (adapterLuid != 0 && adapter == 0)
        {
            return false;
        }

        var result = NativeMethods.D3D11CreateDevice(adapter, driver, 0, MediaFoundationValues.VideoSupport, 0, 0, MediaFoundationValues.D3D11SdkVersion, out device, out _, out context);
        ComObjects.Release(ref adapter);

        if (ComObjects.Failed(result))
        {
            problem = $"D3D11CreateDevice with video support failed (0x{result:X8})";

            return false;
        }

        return true;
    }

    // DXVA under Media Foundation needs the immediate context guarded, the decoder using it from its own threads.
    private static bool TryProtect(ComWrappers wrappers, nint device, out string? problem)
    {
        problem = null;
        var result = Marshal.QueryInterface(device, in MediaFoundationIds.MultithreadInterface, out var multithread);

        if (ComObjects.Failed(result))
        {
            problem = $"the D3D11 device has no ID3D10Multithread (0x{result:X8})";

            return false;
        }

        var api = ComObjects.Wrap<ID3D10Multithread>(wrappers, multithread);
        _ = api.SetMultithreadProtected(1);
        ComObjects.Free(api);
        ComObjects.Release(ref multithread);

        return true;
    }

    /// <summary>A device manager over <paramref name="device"/> (the decoder's, or for its encoder); the caller releases it.</summary>
    internal static bool TryCreateManager(ComWrappers wrappers, nint device, out nint manager, out string? problem)
    {
        problem = null;
        var result = NativeMethods.MFCreateDXGIDeviceManager(out var token, out manager);

        if (ComObjects.Failed(result))
        {
            problem = $"MFCreateDXGIDeviceManager failed (0x{result:X8})";

            return false;
        }

        var api = ComObjects.Wrap<IMFDXGIDeviceManager>(wrappers, manager);
        result = api.ResetDevice(device, token);
        ComObjects.Free(api);

        if (ComObjects.Failed(result))
        {
            ComObjects.Release(ref manager);
            problem = $"IMFDXGIDeviceManager.ResetDevice failed (0x{result:X8})";

            return false;
        }

        return true;
    }

    // A staging texture of the decoder's surface size (one slice, CPU-readable), made only when the size changes.
    private bool TryPrepareStaging(nint texture)
    {
        var api = ComObjects.Wrap<ID3D11Texture2D>(_wrappers, texture);
        api.GetDesc(out var source);
        ComObjects.Free(api);

        if (source.Format != MediaFoundationValues.Nv12Format)
        {
            return false;
        }

        if (_staging != 0 && source.Width == _stagingDescription.Width && source.Height == _stagingDescription.Height)
        {
            _stagingSource = texture;

            return true;
        }

        var description = new Texture2DDescription
        {
            Width = source.Width,
            Height = source.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = MediaFoundationValues.Nv12Format,
            SampleCount = 1,
            Usage = MediaFoundationValues.StagingUsage,
            CpuAccessFlags = MediaFoundationValues.CpuRead,
        };

        ComObjects.Release(ref _staging);

        if (ComObjects.Failed(_deviceApi.CreateTexture2D(in description, 0, out _staging)))
        {
            return false;
        }

        _stagingDescription = description;
        _stagingSource = texture;

        return true;
    }
}
