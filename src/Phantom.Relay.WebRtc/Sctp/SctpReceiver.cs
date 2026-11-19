// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Collections;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The receiving half of an association (RFC 9260 section 6, RFC 3758): DATA chunks land in a ring of fixed slots by
/// TSN, whole messages leave in stream order (or at once when unordered) for a ring of messages the reader drains, and
/// SACKs report the cumulative TSN, the gaps and the duplicates. FORWARD TSN skips what the peer gave up. Everything is
/// sized once, so steady receiving allocates nothing.
/// </summary>
internal sealed class SctpReceiver
{
    /// <summary>Ahead of each delivered message: its stream (2 bytes) and payload protocol identifier (4 bytes).</summary>
    private const int DeliveryHeaderLength = 6;

    private const int DeliveryProtocolOffset = 2;

    /// <summary>RFC 9260 section 6.2: a SACK at least for every second packet that carried DATA.</summary>
    private const int AckEveryPackets = 2;

    private const int MaxGapBlocks = 64;
    private const int MaxDuplicates = 16;
    private const int SackWindowOffset = 4;
    private const int SackGapCountOffset = 8;
    private const int SackDuplicateCountOffset = 10;
    private const int GapEndOffset = 2;

    /// <summary>Once the window advertised fell below half, a SACK announces when it is back above half.</summary>
    private const int WindowUpdateDivisor = 2;

    private readonly SctpInSlot[] _slots;
    private readonly byte[] _data;
    private readonly uint _mask;
    private readonly int _chunkLength;
    private readonly ushort[] _nextSequence;
    private readonly ByteRing _delivery;
    private readonly uint[] _duplicates = new uint[MaxDuplicates];
    private readonly long _capacity;
    private readonly long _delayedAck;

    private uint _cumulative;
    private uint _highest;
    private uint _base;
    private long _heldBytes;
    private int _duplicateCount;
    private bool _sackNow;
    private long? _ackDueAt;
    private int _unackedPackets;
    private bool _dataInPacket;
    private long _lastAdvertised;

    public SctpReceiver(SctpOptions options, long delayedAck)
    {
        _slots = new SctpInSlot[options.ReceiveSlots];
        _data = new byte[(long)options.ReceiveSlots * options.ReceiveChunkLength];
        _mask = (uint)options.ReceiveSlots - 1;
        _chunkLength = options.ReceiveChunkLength;
        _nextSequence = new ushort[options.Streams];
        _delivery = new ByteRing(options.DeliveryBufferLength);

        _capacity = options.ReceiveWindow;
        _delayedAck = delayedAck;
        _lastAdvertised = _capacity;
    }

    /// <summary>The highest TSN up to which every chunk arrived or was skipped.</summary>
    public uint Cumulative => _cumulative;

    /// <summary>The receive window to advertise: the receive buffer's free bytes.</summary>
    public uint Window => (uint)Math.Clamp(_capacity - _heldBytes, 0, uint.MaxValue);

    public void Start(uint peerInitialTsn)
    {
        Array.Clear(_slots);
        Array.Clear(_nextSequence);
        _cumulative = peerInitialTsn - 1;
        _highest = _cumulative;
        _base = peerInitialTsn;
        _heldBytes = 0;
    }

    /// <summary>Takes a DATA chunk: drops duplicates and what lies past the window, holds the rest.</summary>
    [HotPath]
    public void OnData(byte flags, ReadOnlySpan<byte> value)
    {
        if (value.Length <= SctpFormat.DataFixedLength)
        {
            return;
        }

        uint tsn = BinaryPrimitives.ReadUInt32BigEndian(value);
        ushort stream = BinaryPrimitives.ReadUInt16BigEndian(value[SctpFormat.DataStreamOffset..]);
        int length = value.Length - SctpFormat.DataFixedLength;

        if (!SctpSerial.After(tsn, _cumulative) || IsReceived(tsn))
        {
            RecordDuplicate(tsn);

            return;
        }

        _sackNow |= tsn != _cumulative + 1 || _highest != _cumulative;

        if (tsn - _base >= _slots.Length || stream >= _nextSequence.Length || length > _chunkLength)
        {
            return;
        }

        Hold(tsn, flags, value);
        _dataInPacket = true;

        if (SctpSerial.After(tsn, _highest))
        {
            _highest = tsn;
        }

        AdvanceCumulative();
    }

    /// <summary>Ends a packet: decides whether its DATA is acknowledged at once or within the delayed-ack time.</summary>
    [HotPath]
    public void EndPacket(long now)
    {
        if (!_dataInPacket)
        {
            return;
        }

        _dataInPacket = false;
        _unackedPackets++;

        if (_unackedPackets >= AckEveryPackets || _highest != _cumulative)
        {
            _sackNow = true;
        }
        else
        {
            _ackDueAt ??= now + _delayedAck;
        }
    }

    /// <summary>Takes a FORWARD TSN: drops the given-up fragments, moves the cumulative TSN and each skipped stream on.</summary>
    [HotPath]
    public void OnForwardTsn(ReadOnlySpan<byte> value)
    {
        if (value.Length < SctpFormat.ForwardTsnFixedLength)
        {
            return;
        }

        _sackNow = true;
        uint cumulative = BinaryPrimitives.ReadUInt32BigEndian(value);

        if (!SctpSerial.After(cumulative, _cumulative))
        {
            return;
        }

        DropIncomplete(cumulative);
        _cumulative = cumulative;

        if (SctpSerial.After(cumulative, _highest))
        {
            _highest = cumulative;
        }

        AdvanceCumulative();
        SkipStreams(value[SctpFormat.ForwardTsnFixedLength..]);
        ReleaseBase();
    }

    [HotPath]
    public bool SackDue(long now)
    {
        return _sackNow || now >= _ackDueAt;
    }

    /// <summary>Adds a SACK: the cumulative TSN, the window, up to 64 gap blocks and the duplicates seen since the last.</summary>
    [HotPath]
    public void WriteSack(SctpPacketBuilder builder)
    {
        int gaps = CountGaps();
        int length = SctpFormat.SackFixedLength + (gaps * SctpFormat.GapBlockLength) + (_duplicateCount * SctpFormat.DuplicateTsnLength);
        var value = builder.AddChunk(SctpFormat.Sack, 0, length);
        _lastAdvertised = Window;
        BinaryPrimitives.WriteUInt32BigEndian(value, _cumulative);
        BinaryPrimitives.WriteUInt32BigEndian(value[SackWindowOffset..], (uint)_lastAdvertised);

        BinaryPrimitives.WriteUInt16BigEndian(value[SackGapCountOffset..], (ushort)gaps);
        BinaryPrimitives.WriteUInt16BigEndian(value[SackDuplicateCountOffset..], (ushort)_duplicateCount);
        WriteGaps(value[SctpFormat.SackFixedLength..], gaps);
        WriteDuplicates(value[(SctpFormat.SackFixedLength + (gaps * SctpFormat.GapBlockLength))..]);

        _sackNow = false;
        _ackDueAt = null;
        _unackedPackets = 0;
        _duplicateCount = 0;
    }

    /// <summary>
    /// Moves every whole message whose turn has come to the delivery ring, in TSN order, until the ring is full: an
    /// unordered one at once, an ordered one when it carries its stream's next sequence number (or an earlier one a
    /// FORWARD TSN skipped past).
    /// </summary>
    [HotPath]
    public void Deliver()
    {
        long heldBefore = _heldBytes;
        uint tsn = _base;

        while (!SctpSerial.After(tsn, _highest))
        {
            int fragments = WholeMessageAt(tsn);

            if (fragments == 0)
            {
                tsn++;
                continue;
            }

            if (IsDue(ref Slot(tsn)) && !TryDeliver(tsn, fragments))
            {
                break;
            }

            tsn += (uint)fragments;
        }

        ReleaseBase();

        if (_heldBytes < heldBefore && _lastAdvertised < _capacity / WindowUpdateDivisor && Window >= _capacity / WindowUpdateDivisor)
        {
            _sackNow = true;
        }
    }

    /// <summary>The oldest delivered message, which stays readable until <see cref="Release"/>.</summary>
    [HotPath]
    public bool TryPeek(out ushort stream, out uint protocol, out ReadOnlySpan<byte> payload)
    {
        if (!_delivery.TryPeek(out var message))
        {
            stream = 0;
            protocol = 0;
            payload = default;

            return false;
        }

        stream = BinaryPrimitives.ReadUInt16BigEndian(message);
        protocol = BinaryPrimitives.ReadUInt32BigEndian(message[DeliveryProtocolOffset..]);
        payload = message[DeliveryHeaderLength..];

        return true;
    }

    [HotPath]
    public void Release()
    {
        _delivery.Release();
    }

    private void Hold(uint tsn, byte flags, ReadOnlySpan<byte> value)
    {
        ref var slot = ref Slot(tsn);
        slot.Tsn = tsn;
        slot.Flags = flags;
        slot.Stream = BinaryPrimitives.ReadUInt16BigEndian(value[SctpFormat.DataStreamOffset..]);
        slot.Sequence = BinaryPrimitives.ReadUInt16BigEndian(value[SctpFormat.DataSequenceOffset..]);
        slot.Protocol = BinaryPrimitives.ReadUInt32BigEndian(value[SctpFormat.DataProtocolOffset..]);

        slot.Length = value.Length - SctpFormat.DataFixedLength;
        slot.State = SctpSlotState.Held;
        value[SctpFormat.DataFixedLength..].CopyTo(DataOf(tsn));
        _heldBytes += slot.Length;
    }

    private void AdvanceCumulative()
    {
        while (IsReceived(_cumulative + 1))
        {
            _cumulative++;
        }
    }

    private bool IsReceived(uint tsn)
    {
        ref var slot = ref Slot(tsn);

        return slot.State != SctpSlotState.Empty && slot.Tsn == tsn;
    }

    private void RecordDuplicate(uint tsn)
    {
        _sackNow = true;

        if (_duplicateCount < MaxDuplicates)
        {
            _duplicates[_duplicateCount] = tsn;
            _duplicateCount++;
        }
    }

    /// <summary>The number of fragments when a whole message starts at <paramref name="tsn"/> and is still held; else 0.</summary>
    private int WholeMessageAt(uint tsn)
    {
        ref var first = ref Slot(tsn);

        if (first.State != SctpSlotState.Held || first.Tsn != tsn || (first.Flags & SctpFormat.BeginningFlag) == 0)
        {
            return 0;
        }

        for (uint at = tsn; !SctpSerial.After(at, _highest); at++)
        {
            ref var slot = ref Slot(at);

            if (slot.State != SctpSlotState.Held || slot.Tsn != at || (at != tsn && (slot.Flags & SctpFormat.BeginningFlag) != 0))
            {
                return 0;
            }

            if ((slot.Flags & SctpFormat.EndFlag) != 0)
            {
                return (int)(at - tsn) + 1;
            }
        }

        return 0;
    }

    private bool IsDue(ref SctpInSlot first)
    {
        if ((first.Flags & SctpFormat.UnorderedFlag) != 0)
        {
            return true;
        }

        return !SctpSerial.After(first.Sequence, _nextSequence[first.Stream]);
    }

    /// <summary>Copies a whole message into the delivery ring and frees its slots; false when the ring has no room now.</summary>
    private bool TryDeliver(uint tsn, int fragments)
    {
        ref var first = ref Slot(tsn);
        int length = 0;

        for (uint at = tsn; at != tsn + (uint)fragments; at++)
        {
            length += Slot(at).Length;
        }

        if (length > _delivery.MaxMessageLength - DeliveryHeaderLength)
        {
            // Larger than the ring can ever hold: dropped rather than blocking every later message.
            Free(tsn, fragments);

            return true;
        }

        if (!_delivery.TryReserve(DeliveryHeaderLength + length, out var destination))
        {
            return false;
        }

        BinaryPrimitives.WriteUInt16BigEndian(destination, first.Stream);
        BinaryPrimitives.WriteUInt32BigEndian(destination[DeliveryProtocolOffset..], first.Protocol);
        CopyFragments(tsn, fragments, destination[DeliveryHeaderLength..]);
        _delivery.Commit(DeliveryHeaderLength + length);
        NextInOrder(ref first);
        Free(tsn, fragments);

        return true;
    }

    private void CopyFragments(uint tsn, int fragments, Span<byte> destination)
    {
        int at = 0;

        for (uint fragment = tsn; fragment != tsn + (uint)fragments; fragment++)
        {
            int length = Slot(fragment).Length;
            DataOf(fragment)[..length].CopyTo(destination[at..]);
            at += length;
        }
    }

    private void NextInOrder(ref SctpInSlot first)
    {
        if ((first.Flags & SctpFormat.UnorderedFlag) == 0 && first.Sequence == _nextSequence[first.Stream])
        {
            _nextSequence[first.Stream]++;
        }
    }

    private void Free(uint tsn, int fragments)
    {
        for (uint at = tsn; at != tsn + (uint)fragments; at++)
        {
            ref var slot = ref Slot(at);
            _heldBytes -= slot.Length;
            slot.State = SctpSlotState.Done;
        }
    }

    /// <summary>The window's start moves past every chunk the cumulative TSN covers that is no longer held.</summary>
    private void ReleaseBase()
    {
        while (!SctpSerial.After(_base, _cumulative))
        {
            ref var slot = ref Slot(_base);

            if (slot.State == SctpSlotState.Held && slot.Tsn == _base)
            {
                return;
            }

            slot.State = SctpSlotState.Empty;
            _base++;
        }
    }

    /// <summary>Drops every held chunk up to <paramref name="cumulative"/> that belongs to no whole message.</summary>
    private void DropIncomplete(uint cumulative)
    {
        uint tsn = _base;

        while (!SctpSerial.After(tsn, cumulative) && !SctpSerial.After(tsn, _highest))
        {
            int fragments = WholeMessageAt(tsn);

            if (fragments > 0)
            {
                tsn += (uint)fragments;
                continue;
            }

            if (IsReceived(tsn) && Slot(tsn).State == SctpSlotState.Held)
            {
                Free(tsn, 1);
            }

            tsn++;
        }
    }

    private void SkipStreams(ReadOnlySpan<byte> entries)
    {
        for (int offset = 0; offset + SctpFormat.ForwardTsnStreamLength <= entries.Length; offset += SctpFormat.ForwardTsnStreamLength)
        {
            ushort stream = BinaryPrimitives.ReadUInt16BigEndian(entries[offset..]);
            ushort sequence = BinaryPrimitives.ReadUInt16BigEndian(entries[(offset + sizeof(ushort))..]);

            if (stream < _nextSequence.Length && !SctpSerial.After(_nextSequence[stream], sequence))
            {
                _nextSequence[stream] = (ushort)(sequence + 1);
            }
        }
    }

    /// <summary>Counts the runs of received TSNs past the cumulative TSN, up to <see cref="MaxGapBlocks"/>.</summary>
    private int CountGaps()
    {
        int count = 0;
        uint tsn = _cumulative + 1;

        while (count < MaxGapBlocks && TryNextRun(ref tsn, out _, out _))
        {
            count++;
        }

        return count;
    }

    private void WriteGaps(Span<byte> destination, int gaps)
    {
        uint tsn = _cumulative + 1;

        for (int index = 0; index < gaps && TryNextRun(ref tsn, out uint start, out uint end); index++)
        {
            var block = destination[(index * SctpFormat.GapBlockLength)..];
            BinaryPrimitives.WriteUInt16BigEndian(block, (ushort)(start - _cumulative));
            BinaryPrimitives.WriteUInt16BigEndian(block[GapEndOffset..], (ushort)(end - _cumulative));
        }
    }

    private void WriteDuplicates(Span<byte> destination)
    {
        for (int index = 0; index < _duplicateCount; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination[(index * SctpFormat.DuplicateTsnLength)..], _duplicates[index]);
        }
    }

    /// <summary>The next run of received TSNs from <paramref name="tsn"/> on, which then points past it.</summary>
    private bool TryNextRun(ref uint tsn, out uint start, out uint end)
    {
        while (!SctpSerial.After(tsn, _highest) && !IsReceived(tsn))
        {
            tsn++;
        }

        start = tsn;
        end = tsn;

        if (SctpSerial.After(tsn, _highest))
        {
            return false;
        }

        while (!SctpSerial.After(tsn, _highest) && IsReceived(tsn))
        {
            tsn++;
        }

        end = tsn - 1;

        return true;
    }

    private ref SctpInSlot Slot(uint tsn)
    {
        return ref _slots[tsn & _mask];
    }

    private Span<byte> DataOf(uint tsn)
    {
        return _data.AsSpan((int)(tsn & _mask) * _chunkLength, _chunkLength);
    }
}
