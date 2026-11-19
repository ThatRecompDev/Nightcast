// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// D3D11_VIDEO_PROCESSOR_CONTENT_DESC (d3d11.h): what a video processor converts, its frame rates as DXGI_RATIONAL
/// pairs flattened into numerator and denominator fields.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VideoProcessorContentDescription
{
    public uint InputFrameFormat;
    public uint InputRateNumerator;
    public uint InputRateDenominator;
    public uint InputWidth;
    public uint InputHeight;
    public uint OutputRateNumerator;
    public uint OutputRateDenominator;
    public uint OutputWidth;
    public uint OutputHeight;
    public uint Usage;
}
