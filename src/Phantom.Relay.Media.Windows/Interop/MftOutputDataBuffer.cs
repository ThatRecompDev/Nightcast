// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// MFT_OUTPUT_DATA_BUFFER (mftransform.h): the sample a transform writes into (or, when it provides its own, hands
/// back, which the caller then releases) and the events it may attach.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MftOutputDataBuffer
{
    public uint StreamId;
    public nint Sample;
    public uint Status;
    public nint Events;
}
