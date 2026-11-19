// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Core.Collections;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// The handover from the network thread to the audio thread: a fixed set of packet slots, made once, passed by index
/// through two of the engine's lock-free <see cref="SpscRing{T}"/>s. The network thread takes a free slot, copies the
/// packet into it and queues its index; the audio thread takes the index and gives the slot back once it decoded it.
/// Each ring's volatile publish orders the slot's contents before its index, so neither side locks or allocates.
/// </summary>
internal sealed class PacketQueue
{
    private readonly byte[] _payloads;
    private readonly int[] _lengths;
    private readonly ushort[] _sequences;
    private readonly uint[] _timestamps;
    private readonly int[] _samples;
    private readonly long[] _arrivals;
    private readonly SpscRing<int> _free;
    private readonly SpscRing<int> _arrived;

    /// <param name="slots">A power of two: the packets in flight and in the reorder window together.</param>
    public PacketQueue(int slots)
    {
        _free = new SpscRing<int>(slots);
        _arrived = new SpscRing<int>(slots);
        _payloads = new byte[slots * OpusStreamFormat.MaxPayloadBytes];
        _lengths = new int[slots];
        _sequences = new ushort[slots];
        _timestamps = new uint[slots];

        _samples = new int[slots];
        _arrivals = new long[slots];

        for (var slot = 0; slot < slots; slot++)
        {
            _free.TryEnqueue(slot);
        }
    }

    /// <summary>Network thread: queues a copy of the packet; false when every slot is taken.</summary>
    [HotPath]
    public bool TryWrite(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp, int samples, long arrival)
    {
        if (!_free.TryDequeue(out var slot))
        {
            return false;
        }

        payload.CopyTo(_payloads.AsSpan(slot * OpusStreamFormat.MaxPayloadBytes, OpusStreamFormat.MaxPayloadBytes));
        _lengths[slot] = payload.Length;
        _sequences[slot] = sequence;
        _timestamps[slot] = timestamp;
        _samples[slot] = samples;
        _arrivals[slot] = arrival;

        return _arrived.TryEnqueue(slot);
    }

    /// <summary>Audio thread: the next packet that arrived, in arrival order.</summary>
    [HotPath]
    public bool TryTake(out int slot)
    {
        return _arrived.TryDequeue(out slot);
    }

    /// <summary>Audio thread: gives a slot back to the network thread.</summary>
    [HotPath]
    public void Release(int slot)
    {
        _free.TryEnqueue(slot);
    }

    [HotPath]
    public ReadOnlySpan<byte> Payload(int slot)
    {
        return _payloads.AsSpan(slot * OpusStreamFormat.MaxPayloadBytes, _lengths[slot]);
    }

    [HotPath]
    public ushort Sequence(int slot)
    {
        return _sequences[slot];
    }

    [HotPath]
    public uint Timestamp(int slot)
    {
        return _timestamps[slot];
    }

    /// <summary>The samples a channel the packet holds, from its TOC.</summary>
    [HotPath]
    public int Samples(int slot)
    {
        return _samples[slot];
    }

    /// <summary>When it arrived, in the injected clock's timestamp units.</summary>
    [HotPath]
    public long Arrival(int slot)
    {
        return _arrivals[slot];
    }
}
