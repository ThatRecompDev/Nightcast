// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Media.Linux.Interop;

namespace Phantom.Relay.Media.Linux;

/// <summary>
/// FFmpeg's VA-API device (an AVHWDeviceContext in an AVBufferRef) on one DRM render node, and the name its VA driver
/// gives (vaQueryVendorString: "Mesa Gallium driver ... for AMD Radeon ..." on SteamOS's radeonsi).
/// </summary>
internal sealed class VaapiDevice : IDisposable
{
    /// <summary>The first render node: the only GPU of a handheld such as the ROG Ally (the Z1 Extreme's RDNA3).</summary>
    public const string DefaultRenderNode = "/dev/dri/renderD128";

    private const string UnknownDriver = "unknown VA driver";

    private nint _buffer;

    private VaapiDevice(nint buffer, string renderNode, string driver)
    {
        _buffer = buffer;
        RenderNode = renderNode;
        Driver = driver;
    }

    /// <summary>The device's AVBufferRef, the device's own reference (borrowed: take one with av_buffer_ref).</summary>
    public nint Buffer => _buffer;

    /// <summary>The DRM render node the device opened.</summary>
    public string RenderNode { get; }

    /// <summary>The VA driver's own name, or "unknown VA driver" when libva does not say.</summary>
    public string Driver { get; }

    /// <summary>The device on <paramref name="renderNode"/>; null and why when VA-API does not start there.</summary>
    public static VaapiDevice? TryCreate(string renderNode, out string? problem)
    {
        var result = AvUtilMethods.CreateDevice(out var buffer, FfmpegLayout.DeviceTypeVaapi, renderNode, 0, 0);

        if (result < 0 || buffer == 0)
        {
            problem = $"VA-API did not start on {renderNode}: {NativeFields.Describe(result)}";

            return null;
        }

        problem = null;

        return new VaapiDevice(buffer, renderNode, ReadDriver(buffer));
    }

    public void Dispose()
    {
        if (_buffer != 0)
        {
            AvUtilMethods.BufferUnref(ref _buffer);
        }
    }

    // AVBufferRef.data is the AVHWDeviceContext, its hwctx the AVVAAPIDeviceContext, whose display libva names.
    private static string ReadDriver(nint buffer)
    {
        var device = NativeFields.ReadPointer(buffer, FfmpegLayout.BufferRefData);
        var vaapi = NativeFields.ReadPointer(device, FfmpegLayout.DeviceContextHwContext);
        var display = NativeFields.ReadPointer(vaapi, FfmpegLayout.VaapiDeviceDisplay);

        try
        {
            return Marshal.PtrToStringUTF8(VaMethods.QueryVendorString(display)) ?? UnknownDriver;
        }
        catch (DllNotFoundException)
        {
            return UnknownDriver;
        }
        catch (EntryPointNotFoundException)
        {
            return UnknownDriver;
        }
    }
}
