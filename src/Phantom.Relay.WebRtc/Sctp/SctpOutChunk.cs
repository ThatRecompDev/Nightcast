// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// One outgoing DATA chunk in the sender's ring, indexed by its TSN: where its bytes lie in the send buffer, its
/// message's stream, sequence number and policy, and its transmissions. Mutated in place; never boxed.
/// </summary>
internal struct SctpOutChunk
{
    public SctpChunkState State;
    public byte Flags;
    public ushort Stream;
    public ushort Sequence;
    public uint Protocol;

    /// <summary>Where the chunk's bytes start in the send buffer, and how many there are.</summary>
    public int Offset;
    public int Length;

    /// <summary>The send buffer's position just past this chunk: the buffer frees up to it once the chunk is acknowledged.</summary>
    public long BufferEnd;

    public int Transmissions;
    public long SentAt;
    public int MissReports;
    public bool FastRetransmitted;

    public SctpReliability Reliability;
    public uint Limit;
    public long ExpiresAt;

    /// <summary>The TSNs of its message's first and last fragments, abandoned together.</summary>
    public uint MessageFirst;
    public uint MessageLast;
}
