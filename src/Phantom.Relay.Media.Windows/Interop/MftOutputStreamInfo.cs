// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>MFT_OUTPUT_STREAM_INFO (mftransform.h): whether the transform provides its output samples, and their size.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MftOutputStreamInfo
{
    public uint Flags;
    public uint Size;
    public uint Alignment;
}
