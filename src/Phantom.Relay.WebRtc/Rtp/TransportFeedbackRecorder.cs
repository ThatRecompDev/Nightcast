// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Numerics;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// The receiver's side of transport-wide congestion control (draft-holmer-rmcat-transport-wide-cc-extensions-01): it
/// records the arrival of every packet by its transport-wide sequence number, and writes the RTCP transport feedback
/// (section 3.1) of the packets since the last one: the base sequence and status count, the reference time in 64 ms,
/// the feedback packet count, the statuses as run-length and status vector chunks, the receive deltas in 250 µs (one
/// octet when 0 to 63.75 ms, else two, signed), zero-padded to a word. A packet that arrives after the feedback that
/// reported it as lost is not reported again; a delta beyond 16 signed bits ends the feedback before its packet, which
/// the next feedback then starts with. The ring holds <c>capacity</c> packets; older ones are given up unreported.
/// Allocation-free once built.
/// </summary>
public sealed class TransportFeedbackRecorder
{
    private const long Unset = -1;

    private readonly long[] _sequences;
    private readonly long[] _arrivals;
    private readonly byte[] _symbols;
    private readonly short[] _deltas;
    private readonly RtpSequenceUnwrapper _unwrapper = new();
    private readonly long _mask;

    private long _next;
    private long _highest = Unset;
    private bool _started;
    private byte _feedbackCount;

    /// <summary>A recorder for up to <paramref name="capacity"/> packets between feedbacks, a power of two.</summary>
    public TransportFeedbackRecorder(int capacity)
    {
        if (capacity <= 0 || !BitOperations.IsPow2(capacity))
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity is a power of two.");
        }

        _sequences = new long[capacity];
        Array.Fill(_sequences, Unset);
        _arrivals = new long[capacity];
        _symbols = new byte[capacity];
        _deltas = new short[capacity];
        _mask = capacity - 1;
    }

    /// <summary>True when packets arrived that no feedback reported yet.</summary>
    public bool HasPending => _started && _highest >= _next;

    /// <summary>The feedbacks written so far (the count the next one carries, modulo 256).</summary>
    public long FeedbackCount { get; private set; }

    /// <summary>Notes that the packet numbered <paramref name="sequence"/> arrived <paramref name="arrival"/> after the caller's epoch.</summary>
    [HotPath]
    public void Record(ushort sequence, TimeSpan arrival)
    {
        long extended = _unwrapper.Unwrap(sequence);

        if (!_started)
        {
            _started = true;
            _next = extended;
        }

        if (extended < _next)
        {
            return;
        }

        // The ring holds the latest packets; those it no longer holds are given up.
        _next = Math.Max(_next, extended - _mask);
        long slot = extended & _mask;
        _sequences[slot] = extended;
        _arrivals[slot] = arrival.Ticks / TransportFeedbackFormat.DeltaTicks;
        _highest = Math.Max(_highest, extended);
    }

    /// <summary>
    /// The feedback from <paramref name="senderSsrc"/> about <paramref name="mediaSsrc"/>'s transport of the packets
    /// since the last one, as many as fit in <paramref name="destination"/>; its length, or 0 when none are pending.
    /// </summary>
    [HotPath]
    public int Write(Span<byte> destination, uint senderSsrc, uint mediaSsrc)
    {
        if (!HasPending)
        {
            return 0;
        }

        int count = Collect(destination.Length, out long reference);

        if (count == 0)
        {
            return 0;
        }

        int at = WriteChunks(destination, count);
        at = WriteDeltas(destination, at, count);
        at = Pad(destination, at);
        WriteHeader(destination, at, senderSsrc, mediaSsrc, count, reference);

        _next += count;
        _feedbackCount++;
        FeedbackCount++;

        return at;
    }

    /// <summary>
    /// Fills the symbols and deltas from the first unreported packet on, while each delta fits and the packet stays
    /// within <paramref name="room"/>; returns how many, ending with a received packet. The reference time is the first
    /// received packet's arrival in 64 ms units, rounded down.
    /// </summary>
    private int Collect(int room, out long reference)
    {
        reference = Unset;
        long previous = 0;
        int count = 0;
        int deltaBytes = 0;

        for (long sequence = _next; sequence <= _highest; sequence++)
        {
            long slot = sequence & _mask;
            bool received = _sequences[slot] == sequence;

            if (received && reference == Unset)
            {
                reference = _arrivals[slot] / TransportFeedbackFormat.DeltasPerReference;
                previous = reference * TransportFeedbackFormat.DeltasPerReference;
            }

            long delta = received ? _arrivals[slot] - previous : 0;
            byte symbol = SymbolOf(received, delta);
            int size = DeltaLength(symbol);

            if (delta is < short.MinValue or > short.MaxValue || TransportFeedbackFormat.MaxLength(count + 1, deltaBytes + size) > room)
            {
                break;
            }

            _symbols[count] = symbol;
            _deltas[count] = (short)delta;
            count++;
            deltaBytes += size;

            if (received)
            {
                previous = _arrivals[slot];
            }
        }

        return WithoutTrailingLosses(count);
    }

    /// <summary>Losses after the last received packet wait for the next feedback: they may still arrive.</summary>
    private int WithoutTrailingLosses(int count)
    {
        while (count > 0 && _symbols[count - 1] == TransportFeedbackFormat.NotReceived)
        {
            count--;
        }

        return count;
    }

    private int WriteChunks(Span<byte> destination, int count)
    {
        int at = TransportFeedbackFormat.HeaderLength;
        int index = 0;

        while (index < count)
        {
            index += ChunkOf(_symbols.AsSpan(index, count - index), out ushort chunk);
            BinaryPrimitives.WriteUInt16BigEndian(destination[at..], chunk);
            at += TransportFeedbackFormat.ChunkLength;
        }

        return at;
    }

    /// <summary>
    /// The next chunk of <paramref name="symbols"/> and how many it covers: a run of 14 or more as a run length; else
    /// 14 one-bit symbols when none of them needs a large delta; else a run of 7 or more; else 7 two-bit symbols. A
    /// vector at the end may cover fewer than it holds: the status count says where the statuses end.
    /// </summary>
    private static int ChunkOf(ReadOnlySpan<byte> symbols, out ushort chunk)
    {
        int different = symbols.IndexOfAnyExcept(symbols[0]);
        int run = different < 0 ? symbols.Length : different;
        run = Math.Min(run, TransportFeedbackFormat.MaxRunLength);
        var oneBit = symbols[..Math.Min(TransportFeedbackFormat.OneBitSymbols, symbols.Length)];

        if (run >= TransportFeedbackFormat.OneBitSymbols || (run >= TransportFeedbackFormat.TwoBitSymbols && oneBit.Contains(TransportFeedbackFormat.LargeDelta)))
        {
            chunk = (ushort)((symbols[0] << TransportFeedbackFormat.RunSymbolShift) | run);

            return run;
        }

        if (!oneBit.Contains(TransportFeedbackFormat.LargeDelta))
        {
            chunk = OneBitVector(oneBit);

            return oneBit.Length;
        }

        var twoBit = symbols[..Math.Min(TransportFeedbackFormat.TwoBitSymbols, symbols.Length)];
        chunk = TwoBitVector(twoBit);

        return twoBit.Length;
    }

    private static ushort OneBitVector(ReadOnlySpan<byte> symbols)
    {
        int chunk = TransportFeedbackFormat.VectorChunk;

        for (int i = 0; i < symbols.Length; i++)
        {
            chunk |= symbols[i] << (TransportFeedbackFormat.OneBitSymbols - 1 - i);
        }

        return (ushort)chunk;
    }

    private static ushort TwoBitVector(ReadOnlySpan<byte> symbols)
    {
        int chunk = TransportFeedbackFormat.TwoBitVectorChunk;

        for (int i = 0; i < symbols.Length; i++)
        {
            chunk |= symbols[i] << ((TransportFeedbackFormat.TwoBitSymbols - 1 - i) * TransportFeedbackFormat.TwoBitSymbolWidth);
        }

        return (ushort)chunk;
    }

    private int WriteDeltas(Span<byte> destination, int at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_symbols[i] == TransportFeedbackFormat.SmallDelta)
            {
                destination[at] = (byte)_deltas[i];
            }
            else if (_symbols[i] == TransportFeedbackFormat.LargeDelta)
            {
                BinaryPrimitives.WriteInt16BigEndian(destination[at..], _deltas[i]);
            }

            at += DeltaLength(_symbols[i]);
        }

        return at;
    }

    private static int Pad(Span<byte> destination, int at)
    {
        while (at % RtcpFormat.WordLength != 0)
        {
            destination[at] = 0;
            at++;
        }

        return at;
    }

    private void WriteHeader(Span<byte> destination, int length, uint senderSsrc, uint mediaSsrc, int count, long reference)
    {
        destination[0] = RtcpFormat.VersionBits | RtcpFormat.TransportWideFormat;
        destination[1] = RtcpFormat.TransportFeedback;
        BinaryPrimitives.WriteUInt16BigEndian(destination[sizeof(ushort)..], (ushort)((length / RtcpFormat.WordLength) - 1));
        BinaryPrimitives.WriteUInt32BigEndian(destination[RtcpFormat.HeaderLength..], senderSsrc);
        BinaryPrimitives.WriteUInt32BigEndian(destination[(RtcpFormat.HeaderLength + RtcpFormat.SsrcLength)..], mediaSsrc);

        BinaryPrimitives.WriteUInt16BigEndian(destination[TransportFeedbackFormat.BaseSequenceOffset..], (ushort)_next);
        BinaryPrimitives.WriteUInt16BigEndian(destination[TransportFeedbackFormat.StatusCountOffset..], (ushort)count);
        uint time = (uint)(reference & TransportFeedbackFormat.ReferenceTimeMask) << TransportFeedbackFormat.ReferenceTimeShift;
        BinaryPrimitives.WriteUInt32BigEndian(destination[TransportFeedbackFormat.ReferenceTimeOffset..], time | _feedbackCount);
    }

    private static byte SymbolOf(bool received, long delta)
    {
        if (!received)
        {
            return TransportFeedbackFormat.NotReceived;
        }

        if (delta is >= 0 and <= TransportFeedbackFormat.SmallDeltaMax)
        {
            return TransportFeedbackFormat.SmallDelta;
        }

        return TransportFeedbackFormat.LargeDelta;
    }

    private static int DeltaLength(byte symbol)
    {
        if (symbol == TransportFeedbackFormat.SmallDelta)
        {
            return TransportFeedbackFormat.SmallDeltaLength;
        }

        if (symbol == TransportFeedbackFormat.LargeDelta)
        {
            return TransportFeedbackFormat.LargeDeltaLength;
        }

        return 0;
    }
}
