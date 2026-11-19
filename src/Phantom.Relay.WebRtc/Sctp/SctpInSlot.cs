// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// One received DATA chunk in the receiver's ring, indexed by its TSN; its bytes sit in the slot's fixed share of the
/// receive buffer. Mutated in place; never boxed.
/// </summary>
internal struct SctpInSlot
{
    public SctpSlotState State;
    public byte Flags;
    public ushort Stream;
    public ushort Sequence;
    public uint Protocol;
    public uint Tsn;
    public int Length;
}
