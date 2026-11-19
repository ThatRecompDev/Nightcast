// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg;

/// <summary>
/// What both colour converters open on a D3D11 device: its video side (ID3D11VideoDevice and the immediate context's
/// ID3D11VideoContext), and a video processor for pictures of one size at a nominal rate.
/// </summary>
internal static class VideoProcessorSides
{
    // The frame rate the content description states; the processor converts one frame per call either way.
    private const uint NominalRate = 60;

    /// <summary>The device's video device and its immediate context's video context (references the caller releases); false and why when it has none.</summary>
    public static bool TryOpen(ComWrappers wrappers, nint device, out nint video, out nint context, out string? problem)
    {
        context = 0;
        var result = Marshal.QueryInterface(device, in Direct3D11Values.VideoDeviceInterface, out video);

        if (ComObjects.Failed(result))
        {
            problem = $"the D3D11 device has no video side (0x{result:X8}); create it with D3D11_CREATE_DEVICE_VIDEO_SUPPORT";

            return false;
        }

        var deviceApi = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        deviceApi.GetImmediateContext(out var immediate);
        ComObjects.Free(deviceApi);
        result = Marshal.QueryInterface(immediate, in Direct3D11Values.VideoContextInterface, out context);
        ComObjects.Release(ref immediate);

        if (ComObjects.Failed(result))
        {
            ComObjects.Release(ref video);
            problem = $"the D3D11 context has no video side (0x{result:X8})";

            return false;
        }

        problem = null;

        return true;
    }

    /// <summary>A processor's enumerator and the processor, for progressive pictures of <paramref name="width"/> x <paramref name="height"/> both sides.</summary>
    public static bool TryCreateProcessor(ID3D11VideoDevice video, int width, int height, out nint enumerator, out nint processor)
    {
        processor = 0;
        var content = new VideoProcessorContentDescription
        {
            InputFrameFormat = Direct3D11Values.ProgressiveFrames,
            InputRateNumerator = NominalRate,
            InputRateDenominator = 1,
            InputWidth = (uint)width,
            InputHeight = (uint)height,
            OutputRateNumerator = NominalRate,
            OutputRateDenominator = 1,
            OutputWidth = (uint)width,
            OutputHeight = (uint)height,
            Usage = Direct3D11Values.OptimalSpeedUsage,
        };

        return !ComObjects.Failed(video.CreateVideoProcessorEnumerator(in content, out enumerator)) && !ComObjects.Failed(video.CreateVideoProcessor(enumerator, 0, out processor));
    }
}
