// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// D3D11_VIDEO_PROCESSOR_STREAM (d3d11.h): one input of a video processor blit. Only the current surface is used;
/// the past, future and right-eye lists stay empty.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VideoProcessorStream
{
    public int Enable;
    public uint OutputIndex;
    public uint InputFrameOrField;
    public uint PastFrames;
    public uint FutureFrames;
    public nint PastSurfaces;
    public nint InputSurface;
    public nint FutureSurfaces;
    public nint PastSurfacesRight;
    public nint InputSurfaceRight;
    public nint FutureSurfacesRight;
}
