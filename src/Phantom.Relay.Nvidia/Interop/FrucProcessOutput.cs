// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// NvOFFRUC_PROCESS_OUT_PARAMS (NvOFFRUC.h): the output frame (its texture and the time stamp to interpolate at), the
/// SyncSignal union (with a fence: the value FRUC sets once the output is written, in <see cref="SignalValue"/>; with
/// keyed mutexes: the two release keys), then 32 reserved words; 304 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FrucProcessOutput
{
    public FrucFrameData Frame;
    public ulong SignalValue;
    public ulong SignalSecondKey;
    public fixed uint Reserved[FrucValues.ReservedWords];
}
