// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The sound's packets between the network thread and a recorder's writer: <see cref="TryAdd"/> copies a packet into
/// a slot made in advance and never waits (a full ring, or a packet larger than a slot, is dropped and counted);
/// <see cref="TryTake"/> copies the oldest out. Its slots are made once; nothing is allocated after. One short lock.
/// </summary>
internal sealed class AudioPacketRing
{
    private readonly object _gate = new();
    private readonly byte[] _bytes;
    private readonly int[] _lengths;
    private readonly uint[] _timestamps;
    private readonly long[] _arrivals;
    private readonly int _slotLength;
    private int _head;
    private int _count;
    private long _dropped;

    public AudioPacketRing(int capacity, int slotLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slotLength);

        _slotLength = slotLength;
        _bytes = new byte[capacity * slotLength];
        _lengths = new int[capacity];
        _timestamps = new uint[capacity];
        _arrivals = new long[capacity];
    }

    /// <summary>Packets refused: the ring full, or a packet larger than a slot.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Copies <paramref name="packet"/> in with its RTP timestamp and its arrival (the clock's timestamp).</summary>
    [HotPath]
    public bool TryAdd(ReadOnlySpan<byte> packet, uint timestamp, long arrival)
    {
        lock (_gate)
        {
            if (_count == _lengths.Length || packet.Length > _slotLength)
            {
                _dropped++;

                return false;
            }

            var slot = (_head + _count) % _lengths.Length;
            packet.CopyTo(_bytes.AsSpan(slot * _slotLength, _slotLength));
            _lengths[slot] = packet.Length;
            _timestamps[slot] = timestamp;
            _arrivals[slot] = arrival;
            _count++;
        }

        return true;
    }

    /// <summary>Copies the oldest packet into <paramref name="packet"/> (a slot's length at least); its length, or -1 when none.</summary>
    public int TryTake(Span<byte> packet, out uint timestamp, out long arrival)
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                (timestamp, arrival) = (0, 0);

                return -1;
            }

            var length = _lengths[_head];
            _bytes.AsSpan(_head * _slotLength, length).CopyTo(packet);
            (timestamp, arrival) = (_timestamps[_head], _arrivals[_head]);
            _head = (_head + 1) % _lengths.Length;
            _count--;

            return length;
        }
    }
}
