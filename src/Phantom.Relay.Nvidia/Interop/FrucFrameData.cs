// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// NvOFFRUC_FRAMEDATA (NvOFFRUC.h): a frame (an ID3D11Texture2D registered with FRUC), its time stamp, the CUDA pitch
/// (unused with DirectX) and, for the output, where FRUC says it gave the previous frame back instead of an
/// interpolated one (a C++ bool the caller owns); then 32 reserved words; 160 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FrucFrameData
{
    public nint Frame;
    public double TimeStamp;
    public nuint CudaPitch;
    public byte* RepetitionOccurred;
    public fixed uint Reserved[FrucValues.ReservedWords];
}
