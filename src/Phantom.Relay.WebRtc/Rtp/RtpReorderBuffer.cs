// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// The jitter stage, tuned for latency: it never delays a packet that is in order. The next expected packet goes to the
/// sink at once, from the caller's buffer; only a packet that arrives ahead of a hole is copied and held, for at most
/// the reorder window (a few milliseconds) or until it is <c>capacity</c> packets ahead. Then the hole is given up as
/// lost and the held packets follow in order. A packet behind the next expected one is dropped as late. Holes are
/// reported as they open (<see cref="TakeMissing"/>) so a NACK can go out at once; a hole filled by a reordered packet
/// before it is taken is not reported. Slots are made up front; nothing is allocated per packet.
/// </summary>
public sealed class RtpReorderBuffer
{
    private readonly IRtpPacketSink _sink;
    private readonly TimeProvider _clock;
    private readonly long _windowTicks;
    private readonly int _capacity;
    private readonly byte[][] _slots;
    private readonly int[] _lengths;
    private readonly long[] _sequences;
    private readonly long[] _arrivals;
    private readonly ushort[] _missing;
    private readonly RtpSequenceUnwrapper _unwrapper = new();
    private int _missingCount;
    private bool _started;
    private long _next;
    private long _highest;
    private int _held;

    public RtpReorderBuffer(IRtpPacketSink sink, TimeProvider clock, TimeSpan window, int capacity)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _sink = sink;
        _clock = clock;
        _windowTicks = window.Ticks * clock.TimestampFrequency / TimeSpan.TicksPerSecond;
        _capacity = capacity;

        // One slot per packet that may wait, each the size of the largest packet; the missing list as long.
        _slots = new byte[capacity][];
        _lengths = new int[capacity];
        _sequences = new long[capacity];
        _arrivals = new long[capacity];
        _missing = new ushort[capacity];

        for (int i = 0; i < capacity; i++)
        {
            _slots[i] = new byte[RtpFormat.MaxPacketLength];
        }
    }

    /// <summary>Packets handed to the sink.</summary>
    public long Delivered { get; private set; }

    /// <summary>Packets that came after their place was passed (given up, or already delivered), and dropped.</summary>
    public long Late { get; private set; }

    /// <summary>Packets given up as lost when the window ran out.</summary>
    public long Lost { get; private set; }

    /// <summary>Packets waiting behind a hole.</summary>
    public int Held => _held;

    /// <summary>Takes one received RTP packet (unprotected); delivers it and any it frees, or holds it behind a hole.</summary>
    [HotPath]
    public void Push(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < RtpFormat.FixedHeaderLength)
        {
            return;
        }

        long sequence = _unwrapper.Unwrap(BinaryPrimitives.ReadUInt16BigEndian(packet[RtpFormat.SequenceOffset..]));
        Start(sequence);
        Forget(sequence);

        if (sequence < _next || IsHeld(sequence))
        {
            Late++;

            return;
        }

        if (sequence == _next)
        {
            Deliver(packet, sequence);
            Release();
        }
        else
        {
            Hold(packet, sequence);
        }

        Expire();
    }

    /// <summary>Gives up the holes older than the window; call it every tick so held packets never wait longer.</summary>
    [HotPath]
    public void Service()
    {
        Expire();
    }

    /// <summary>Moves the sequence numbers of newly opened holes, oldest first, into <paramref name="destination"/>.</summary>
    [HotPath]
    public int TakeMissing(Span<ushort> destination)
    {
        int count = Math.Min(_missingCount, destination.Length);
        _missing.AsSpan(0, count).CopyTo(destination);
        _missing.AsSpan(count, _missingCount - count).CopyTo(_missing);
        _missingCount -= count;

        return count;
    }

    private void Start(long sequence)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _next = sequence;
        _highest = sequence - 1;
    }

    private void Deliver(ReadOnlySpan<byte> packet, long sequence)
    {
        _sink.Deliver(packet, sequence);
        _next = sequence + 1;
        _highest = Math.Max(_highest, sequence);
        Delivered++;
    }

    /// <summary>Copies a packet that arrived ahead of a hole, first giving up holes it is too far ahead of.</summary>
    private void Hold(ReadOnlySpan<byte> packet, long sequence)
    {
        while (sequence - _next >= _capacity)
        {
            GiveUp(sequence - _capacity + 1);
        }

        if (sequence == _next)
        {
            Deliver(packet, sequence);
            Release();

            return;
        }

        if (packet.Length > RtpFormat.MaxPacketLength)
        {
            Late++;

            return;
        }

        int slot = Slot(sequence);
        packet.CopyTo(_slots[slot]);
        _lengths[slot] = packet.Length;
        _sequences[slot] = sequence;
        _arrivals[slot] = _clock.GetTimestamp();
        _held++;

        Report(sequence);
    }

    /// <summary>Delivers the held packets that now follow in order.</summary>
    private void Release()
    {
        while (_held > 0 && IsHeld(_next))
        {
            DeliverHeld();
        }
    }

    private void DeliverHeld()
    {
        int slot = Slot(_next);
        int length = _lengths[slot];
        _lengths[slot] = 0;
        _held--;
        Deliver(_slots[slot].AsSpan(0, length), _next);
    }

    /// <summary>Gives up every hole whose first packet after it has waited the whole window.</summary>
    private void Expire()
    {
        long now = _clock.GetTimestamp();

        while (_held > 0)
        {
            long first = FirstHeld();

            if (now - _arrivals[Slot(first)] < _windowTicks)
            {
                return;
            }

            GiveUp(first);
        }
    }

    /// <summary>Moves the next expected packet up to <paramref name="sequence"/>: held packets go out, holes count lost.</summary>
    private void GiveUp(long sequence)
    {
        while (_next < sequence)
        {
            if (IsHeld(_next))
            {
                DeliverHeld();
            }
            else
            {
                Lost++;
                _next++;
            }
        }

        Release();
    }

    private long FirstHeld()
    {
        for (long sequence = _next; sequence <= _highest; sequence++)
        {
            if (IsHeld(sequence))
            {
                return sequence;
            }
        }

        return _highest;
    }

    /// <summary>Notes the holes a packet ahead of the highest opens, for the NACK.</summary>
    private void Report(long sequence)
    {
        for (long missing = _highest + 1; missing < sequence && _missingCount < _missing.Length; missing++)
        {
            _missing[_missingCount] = (ushort)missing;
            _missingCount++;
        }

        _highest = Math.Max(_highest, sequence);
    }

    /// <summary>A packet that fills a hole is no longer missing.</summary>
    private void Forget(long sequence)
    {
        var missing = _missing.AsSpan(0, _missingCount);
        int at = missing.IndexOf((ushort)sequence);

        if (at < 0)
        {
            return;
        }

        missing[(at + 1)..].CopyTo(missing[at..]);
        _missingCount--;
    }

    private bool IsHeld(long sequence)
    {
        int slot = Slot(sequence);

        return _lengths[slot] != 0 && _sequences[slot] == sequence;
    }

    private int Slot(long sequence)
    {
        return (int)(((sequence % _capacity) + _capacity) % _capacity);
    }
}
