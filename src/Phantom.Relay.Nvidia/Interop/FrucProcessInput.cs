// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// NvOFFRUC_PROCESS_IN_PARAMS (NvOFFRUC.h): the new frame, the bSkipWarp bit (state update only, no output), the
/// SyncWait union (with a fence: the value FRUC waits for before it reads the frame, in <see cref="WaitValue"/>; with
/// keyed mutexes: the two acquire keys), then 32 reserved words; 312 bytes (the union starts 8-aligned at 168).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FrucProcessInput
{
    public FrucFrameData Frame;
    public uint Flags;
    public ulong WaitValue;
    public ulong WaitSecondKey;
    public fixed uint Reserved[FrucValues.ReservedWords];
}
