// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The sizes an association allocates once, when it is made, and never grows.
/// </summary>
/// <param name="MaxPacketLength">The largest packet sent, which DTLS then carries in one datagram.</param>
/// <param name="SendChunks">DATA chunks queued or unacknowledged at once; a power of two.</param>
/// <param name="SendBufferLength">Bytes of messages queued or unacknowledged at once.</param>
/// <param name="ReceiveSlots">DATA chunks held for reassembly and ordering at once; a power of two.</param>
/// <param name="ReceiveChunkLength">The most user data one received DATA chunk may carry.</param>
/// <param name="DeliveryBufferLength">Bytes of whole messages waiting to be read; a power of two.</param>
/// <param name="Streams">Streams announced both ways.</param>
public sealed record SctpOptions(
    int MaxPacketLength,
    int SendChunks,
    int SendBufferLength,
    int ReceiveSlots,
    int ReceiveChunkLength,
    int DeliveryBufferLength,
    int Streams)
{
    /// <summary>The smallest packet that still holds a SACK with all its gap reports and any one control chunk.</summary>
    public const int MinPacketLength = 512;

    /// <summary>
    /// WebRTC's sizes: 1200-byte packets, which fit every path's MTU after DTLS, UDP and IP; a receive window of
    /// 1.5 MB (1024 chunks of up to 1500 bytes), large enough that the window never limits the peer on a home network;
    /// and 1024 streams, the most a browser opens.
    /// </summary>
    public static SctpOptions Default { get; } = new(1200, 4096, 1 << 20, 1024, 1500, 1 << 18, 1024);

    /// <summary>The bytes the receive window advertises when nothing is held.</summary>
    public long ReceiveWindow => (long)ReceiveSlots * ReceiveChunkLength;

    /// <summary>Throws when a size is out of range or a power of two is not one.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxPacketLength, MinPacketLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(SendBufferLength, MaxPacketLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(ReceiveChunkLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Streams, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Streams, ushort.MaxValue + 1);

        if (!BitOperations.IsPow2(SendChunks) || !BitOperations.IsPow2(ReceiveSlots) || !BitOperations.IsPow2(DeliveryBufferLength))
        {
            throw new ArgumentException("SendChunks, ReceiveSlots and DeliveryBufferLength must be powers of two");
        }
    }
}
