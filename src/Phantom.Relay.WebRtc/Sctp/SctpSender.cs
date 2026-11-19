// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The sending half of an association (RFC 9260 sections 6 and 7, RFC 3758): messages are copied into one send
/// buffer and cut into DATA chunks, numbered with TSNs, when they are queued; chunks go out within the congestion and
/// receiver windows; SACKs release them, gap reports and the retransmission timer resend them, and a message whose
/// policy gives up is skipped at the peer with FORWARD TSN. Everything lives in rings sized once, so steady sending
/// allocates nothing.
/// </summary>
internal sealed class SctpSender
{
    private const int FastRetransmitReports = 3;

    /// <summary>RFC 9260 section 7.2.1: the initial window is min(4 MTU, max(2 MTU, 4404 bytes)).</summary>
    private const int InitialWindowPackets = 4;

    private const int SmallestWindowPackets = 2;
    private const int InitialWindowBytes = 4404;

    /// <summary>After a loss the slow-start threshold is max(cwnd / 2, 4 MTU).</summary>
    private const int ThresholdFloorPackets = 4;

    private const int HalfShift = 1;
    private const int MaxForwardStreams = 64;
    private const int GapCountOffset = 8;
    private const int GapEndOffset = 2;

    private readonly SctpOutChunk[] _chunks;
    private readonly uint _mask;
    private readonly byte[] _buffer;
    private readonly ushort[] _sequences;
    private readonly ushort[] _forwardStreams = new ushort[MaxForwardStreams];
    private readonly ushort[] _forwardSequences = new ushort[MaxForwardStreams];
    private readonly SctpRto _rto;
    private readonly int _mtu;
    private readonly int _maxFragment;
    private readonly long _ticksPerMillisecond;

    private long _bufferHead;
    private long _bufferTail;
    private uint _cumulative;
    private uint _nextTsn;
    private uint _sendCursor;
    private uint _advancedAck;
    private int _flight;
    private int _window;
    private int _threshold;
    private int _partialBytesAcked;
    private long _peerWindow;
    private int _retransmits;
    private long? _timerAt;
    private long _nextExpiry = long.MaxValue;
    private bool _fastRecovery;
    private uint _recoverTsn;
    private bool _forwardTsnDue;
    private bool _forwardTsnSupported;
    private bool _packetFull;
    private int _newlyAcked;
    private long? _sampleSentAt;
    private uint _highestNewlyAcked;

    public SctpSender(SctpOptions options, SctpRto rto, long ticksPerMillisecond)
    {
        _chunks = new SctpOutChunk[options.SendChunks];
        _mask = (uint)options.SendChunks - 1;
        _buffer = new byte[options.SendBufferLength];
        _sequences = new ushort[options.Streams];
        _rto = rto;
        _mtu = options.MaxPacketLength;

        _maxFragment = options.MaxPacketLength - SctpFormat.CommonHeaderLength - SctpFormat.ChunkHeaderLength - SctpFormat.DataFixedLength;
        _ticksPerMillisecond = ticksPerMillisecond;
    }

    /// <summary>Streams the peer takes from this side.</summary>
    public int OutboundStreams { get; private set; }

    /// <summary>Timeouts in a row without an acknowledgement in between.</summary>
    public int ErrorCount { get; private set; }

    /// <summary>True when every queued chunk is acknowledged or skipped at the peer.</summary>
    public bool IsIdle => _cumulative == _nextTsn - 1;

    /// <summary>Bytes of messages queued or waiting for an acknowledgement.</summary>
    public long BufferedBytes => _bufferTail - _bufferHead;

    public int CongestionWindow => _window;

    public void Start(uint initialTsn, uint peerWindow, bool forwardTsnSupported, int outboundStreams)
    {
        Array.Clear(_chunks);
        Array.Clear(_sequences);
        _cumulative = initialTsn - 1;
        _advancedAck = _cumulative;
        _nextTsn = initialTsn;
        _sendCursor = initialTsn;

        _window = Math.Min(InitialWindowPackets * _mtu, Math.Max(SmallestWindowPackets * _mtu, InitialWindowBytes));
        _threshold = (int)Math.Min(peerWindow, int.MaxValue);
        _peerWindow = peerWindow;
        _forwardTsnSupported = forwardTsnSupported;
        OutboundStreams = outboundStreams;
    }

    /// <summary>Copies a message in and cuts it into chunks; false when it is empty, its stream unknown or the buffers full.</summary>
    [HotPath]
    public bool Enqueue(ushort stream, uint protocol, ReadOnlySpan<byte> payload, SctpSendPolicy policy, long now)
    {
        if (payload.IsEmpty || stream >= OutboundStreams)
        {
            return false;
        }

        int fragments = (payload.Length + _maxFragment - 1) / _maxFragment;

        if ((long)(_nextTsn - _cumulative - 1) + fragments > _chunks.Length || !TryReserve(payload.Length, out int offset, out long start))
        {
            return false;
        }

        payload.CopyTo(_buffer.AsSpan(offset));
        var message = Template(stream, protocol, policy, now);
        message.MessageFirst = _nextTsn;
        message.MessageLast = _nextTsn + (uint)fragments - 1;

        for (int index = 0; index < fragments; index++)
        {
            int from = index * _maxFragment;
            ref var chunk = ref Chunk(_nextTsn);
            chunk = message;
            chunk.Offset = offset + from;
            chunk.Length = Math.Min(_maxFragment, payload.Length - from);
            chunk.BufferEnd = start + from + chunk.Length;

            chunk.Flags = FragmentFlags(index, fragments, policy.Unordered);
            _nextTsn++;
        }

        return true;
    }

    /// <summary>Takes a SACK: releases what it acknowledges, counts misses, grows the window and restarts the timer.</summary>
    [HotPath]
    public void OnSack(ReadOnlySpan<byte> value, long now)
    {
        if (value.Length < SctpFormat.SackFixedLength)
        {
            return;
        }

        uint cumulative = BinaryPrimitives.ReadUInt32BigEndian(value);

        if (SctpSerial.After(_cumulative, cumulative) || !SctpSerial.After(_nextTsn, cumulative))
        {
            return;
        }

        int flightBefore = _flight;
        _newlyAcked = 0;
        _sampleSentAt = null;
        _highestNewlyAcked = cumulative;
        bool advanced = AcknowledgeThrough(cumulative);
        AcknowledgeGaps(value, cumulative);

        // What the round trip, the losses and the windows learn from it.
        CountMisses(cumulative);
        Learn(advanced, flightBefore, now);
        uint window = BinaryPrimitives.ReadUInt32BigEndian(value[sizeof(uint)..]);
        _peerWindow = Math.Max(0, window - _flight);
        AdvancePeerAckPoint();

        if (_forwardTsnSupported && SctpSerial.After(_advancedAck, cumulative))
        {
            _forwardTsnDue = true;
        }
    }

    /// <summary>Takes the cumulative TSN a SHUTDOWN carries, which acknowledges like a SACK without gaps.</summary>
    [HotPath]
    public void OnCumulativeAck(uint cumulative, long now)
    {
        if (!SctpSerial.After(cumulative, _cumulative) || !SctpSerial.After(_nextTsn, cumulative))
        {
            return;
        }

        int flightBefore = _flight;
        _newlyAcked = 0;
        _sampleSentAt = null;
        bool advanced = AcknowledgeThrough(cumulative);
        Learn(advanced, flightBefore, now);
    }

    /// <summary>
    /// Gives up messages whose lifetime ran out and handles the retransmission timer; false once timeouts in a row
    /// exceed <see cref="SctpTiming.MaxAssociationRetransmits"/>, which ends the association.
    /// </summary>
    [HotPath]
    public bool Expire(long now)
    {
        if (now >= _nextExpiry)
        {
            AbandonExpired(now);
        }

        if (!_timerAt.HasValue || now < _timerAt.Value)
        {
            return true;
        }

        // A timeout: the window collapses to one packet and the timeout doubles.
        _timerAt = null;
        ErrorCount++;
        _threshold = Math.Max(_window >> HalfShift, ThresholdFloorPackets * _mtu);
        _window = _mtu;
        _partialBytesAcked = 0;
        _fastRecovery = false;

        _rto.Backoff();

        // Everything in flight is taken as lost and goes again, first.
        for (uint tsn = _cumulative + 1; SctpSerial.After(_sendCursor, tsn); tsn++)
        {
            MarkLost(ref Chunk(tsn));
        }

        _forwardTsnDue = _forwardTsnSupported && SctpSerial.After(_advancedAck, _cumulative);

        return ErrorCount <= SctpTiming.MaxAssociationRetransmits;
    }

    /// <summary>Adds a FORWARD TSN when one is due: the new cumulative TSN and the last skipped sequence per ordered stream.</summary>
    [HotPath]
    public void WriteForwardTsn(SctpPacketBuilder builder, long now)
    {
        if (!_forwardTsnDue)
        {
            return;
        }

        int streams = CollectSkippedStreams();
        int length = SctpFormat.ForwardTsnFixedLength + (streams * SctpFormat.ForwardTsnStreamLength);

        if (!builder.Fits(length))
        {
            return;
        }

        var value = builder.AddChunk(SctpFormat.ForwardTsn, 0, length);
        BinaryPrimitives.WriteUInt32BigEndian(value, _advancedAck);

        for (int index = 0; index < streams; index++)
        {
            var entry = value[(SctpFormat.ForwardTsnFixedLength + (index * SctpFormat.ForwardTsnStreamLength))..];
            BinaryPrimitives.WriteUInt16BigEndian(entry, _forwardStreams[index]);
            BinaryPrimitives.WriteUInt16BigEndian(entry[sizeof(ushort)..], _forwardSequences[index]);
        }

        _forwardTsnDue = false;
        _timerAt ??= now + _rto.Current;
    }

    /// <summary>
    /// Adds DATA chunks to the packet: lost chunks first, then new ones, within the congestion and receiver windows.
    /// True when the packet filled up with more waiting, so the caller sends it and builds another.
    /// </summary>
    [HotPath]
    public bool WriteData(SctpPacketBuilder builder, long now)
    {
        _packetFull = false;

        if (_retransmits > 0 && !WriteRetransmissions(builder, now))
        {
            return _packetFull;
        }

        WriteNew(builder, now);

        return _packetFull;
    }

    private bool WriteRetransmissions(SctpPacketBuilder builder, long now)
    {
        for (uint tsn = _cumulative + 1; _retransmits > 0 && SctpSerial.After(_sendCursor, tsn); tsn++)
        {
            ref var chunk = ref Chunk(tsn);

            if (chunk.State != SctpChunkState.Retransmit)
            {
                continue;
            }

            if (GivesUp(ref chunk, now))
            {
                Abandon(chunk.MessageFirst, chunk.MessageLast);
                continue;
            }

            if (!CanSend(builder, chunk.Length))
            {
                return false;
            }

            _retransmits--;
            Transmit(builder, ref chunk, tsn, now);
        }

        return true;
    }

    private void WriteNew(SctpPacketBuilder builder, long now)
    {
        while (SctpSerial.After(_nextTsn, _sendCursor))
        {
            ref var chunk = ref Chunk(_sendCursor);

            if (chunk.Reliability == SctpReliability.Lifetime && _forwardTsnSupported && now >= chunk.ExpiresAt)
            {
                Abandon(chunk.MessageFirst, chunk.MessageLast);
            }

            if (chunk.State == SctpChunkState.Abandoned)
            {
                _sendCursor++;
                continue;
            }

            if (!CanSend(builder, chunk.Length) || (_flight > 0 && chunk.Length > _peerWindow))
            {
                return;
            }

            Transmit(builder, ref chunk, _sendCursor, now);
            _sendCursor++;
        }
    }

    /// <summary>True when the chunk fits the packet and the congestion window, which always lets one packet out.</summary>
    private bool CanSend(SctpPacketBuilder builder, int length)
    {
        if (!builder.Fits(SctpFormat.DataFixedLength + length))
        {
            _packetFull = builder.HasChunks;

            return false;
        }

        return _flight == 0 || _flight + length <= _window;
    }

    private void Transmit(SctpPacketBuilder builder, ref SctpOutChunk chunk, uint tsn, long now)
    {
        var value = builder.AddChunk(SctpFormat.Data, chunk.Flags, SctpFormat.DataFixedLength + chunk.Length);
        BinaryPrimitives.WriteUInt32BigEndian(value, tsn);
        BinaryPrimitives.WriteUInt16BigEndian(value[SctpFormat.DataStreamOffset..], chunk.Stream);
        BinaryPrimitives.WriteUInt16BigEndian(value[SctpFormat.DataSequenceOffset..], chunk.Sequence);
        BinaryPrimitives.WriteUInt32BigEndian(value[SctpFormat.DataProtocolOffset..], chunk.Protocol);
        _buffer.AsSpan(chunk.Offset, chunk.Length).CopyTo(value[SctpFormat.DataFixedLength..]);

        chunk.State = SctpChunkState.InFlight;
        chunk.Transmissions++;
        chunk.SentAt = now;
        chunk.MissReports = 0;
        _flight += chunk.Length;
        _peerWindow = Math.Max(0, _peerWindow - chunk.Length);

        _timerAt ??= now + _rto.Current;
    }

    /// <summary>Releases every chunk up to <paramref name="cumulative"/>; true when that moved the cumulative TSN.</summary>
    private bool AcknowledgeThrough(uint cumulative)
    {
        if (!SctpSerial.After(cumulative, _cumulative))
        {
            return false;
        }

        for (uint tsn = _cumulative + 1; !SctpSerial.After(tsn, cumulative); tsn++)
        {
            ref var chunk = ref Chunk(tsn);
            Acknowledge(ref chunk);
            _bufferHead = chunk.BufferEnd;
            chunk.State = SctpChunkState.Free;
        }

        _cumulative = cumulative;

        if (SctpSerial.After(_cumulative, _advancedAck))
        {
            _advancedAck = _cumulative;
        }

        if (SctpSerial.After(_cumulative + 1, _sendCursor))
        {
            _sendCursor = _cumulative + 1;
        }

        return true;
    }

    private void AcknowledgeGaps(ReadOnlySpan<byte> value, uint cumulative)
    {
        int blocks = BinaryPrimitives.ReadUInt16BigEndian(value[GapCountOffset..]);
        int offset = SctpFormat.SackFixedLength;

        for (int block = 0; block < blocks && offset + SctpFormat.GapBlockLength <= value.Length; block++)
        {
            uint start = cumulative + BinaryPrimitives.ReadUInt16BigEndian(value[offset..]);
            uint end = cumulative + BinaryPrimitives.ReadUInt16BigEndian(value[(offset + GapEndOffset)..]);
            offset += SctpFormat.GapBlockLength;

            for (uint tsn = start; !SctpSerial.After(tsn, end) && SctpSerial.After(_sendCursor, tsn); tsn++)
            {
                ref var chunk = ref Chunk(tsn);

                if (chunk.State is SctpChunkState.InFlight or SctpChunkState.Retransmit)
                {
                    Acknowledge(ref chunk);
                    chunk.State = SctpChunkState.GapAcked;
                    _highestNewlyAcked = tsn;
                }
            }
        }
    }

    /// <summary>Counts an acknowledged chunk's bytes and takes its round trip when it went out only once (Karn).</summary>
    private void Acknowledge(ref SctpOutChunk chunk)
    {
        if (chunk.State == SctpChunkState.InFlight)
        {
            _flight -= chunk.Length;
        }

        if (chunk.State == SctpChunkState.Retransmit)
        {
            _retransmits--;
        }

        if (chunk.State is SctpChunkState.InFlight or SctpChunkState.Retransmit)
        {
            _newlyAcked += chunk.Length;

            if (chunk.Transmissions == 1)
            {
                _sampleSentAt = chunk.SentAt;
            }
        }
    }

    /// <summary>
    /// A chunk still in flight below the highest TSN this SACK newly acknowledged was missed once more; on the third
    /// miss it is sent again at once (fast retransmit) and the window halves, once per loss event.
    /// </summary>
    private void CountMisses(uint cumulative)
    {
        for (uint tsn = cumulative + 1; SctpSerial.After(_highestNewlyAcked, tsn); tsn++)
        {
            ref var chunk = ref Chunk(tsn);

            if (chunk.State != SctpChunkState.InFlight || chunk.FastRetransmitted)
            {
                continue;
            }

            chunk.MissReports++;

            if (chunk.MissReports >= FastRetransmitReports)
            {
                chunk.FastRetransmitted = true;
                MarkLost(ref chunk);
                EnterFastRecovery();
            }
        }
    }

    private void EnterFastRecovery()
    {
        if (_fastRecovery)
        {
            return;
        }

        _fastRecovery = true;
        _recoverTsn = _sendCursor - 1;
        _threshold = Math.Max(_window >> HalfShift, ThresholdFloorPackets * _mtu);
        _window = _threshold;
        _partialBytesAcked = 0;
    }

    /// <summary>The round trip, the congestion window (slow start, then congestion avoidance) and the timer.</summary>
    private void Learn(bool advanced, int flightBefore, long now)
    {
        if (_newlyAcked > 0)
        {
            ErrorCount = 0;
        }

        if (_sampleSentAt.HasValue)
        {
            _rto.Measure(now - _sampleSentAt.Value);
        }

        if (_fastRecovery && !SctpSerial.After(_recoverTsn, _cumulative))
        {
            _fastRecovery = false;
        }

        if (advanced && !_fastRecovery && flightBefore + _mtu >= _window)
        {
            Grow();
        }

        if (_flight == 0 && _retransmits == 0 && !SctpSerial.After(_advancedAck, _cumulative))
        {
            _timerAt = null;
        }
        else if (advanced)
        {
            _timerAt = now + _rto.Current;
        }
    }

    private void Grow()
    {
        if (_window <= _threshold)
        {
            _window += Math.Min(_newlyAcked, _mtu);

            return;
        }

        _partialBytesAcked += _newlyAcked;

        if (_partialBytesAcked >= _window)
        {
            _partialBytesAcked -= _window;
            _window += _mtu;
        }
    }

    private void MarkLost(ref SctpOutChunk chunk)
    {
        if (chunk.State != SctpChunkState.InFlight)
        {
            return;
        }

        _flight -= chunk.Length;
        chunk.State = SctpChunkState.Retransmit;
        _retransmits++;
    }

    /// <summary>True when the chunk's message policy gives it up instead of sending it again.</summary>
    private bool GivesUp(ref SctpOutChunk chunk, long now)
    {
        if (!_forwardTsnSupported)
        {
            return false;
        }

        if (chunk.Reliability == SctpReliability.MaxRetransmits)
        {
            return chunk.Transmissions > chunk.Limit;
        }

        return chunk.Reliability == SctpReliability.Lifetime && now >= chunk.ExpiresAt;
    }

    /// <summary>Marks every fragment of a message abandoned and moves the point FORWARD TSN announces.</summary>
    private void Abandon(uint first, uint last)
    {
        for (uint tsn = first; !SctpSerial.After(tsn, last); tsn++)
        {
            if (!SctpSerial.After(tsn, _cumulative))
            {
                continue;
            }

            ref var chunk = ref Chunk(tsn);

            if (chunk.State == SctpChunkState.InFlight)
            {
                _flight -= chunk.Length;
            }

            if (chunk.State == SctpChunkState.Retransmit)
            {
                _retransmits--;
            }

            chunk.State = SctpChunkState.Abandoned;
        }

        AdvancePeerAckPoint();
    }

    private void AbandonExpired(long now)
    {
        _nextExpiry = long.MaxValue;

        for (uint tsn = _cumulative + 1; SctpSerial.After(_nextTsn, tsn); tsn++)
        {
            ref var chunk = ref Chunk(tsn);

            if (chunk.Reliability != SctpReliability.Lifetime || chunk.State is SctpChunkState.Abandoned or SctpChunkState.GapAcked)
            {
                continue;
            }

            if (now >= chunk.ExpiresAt)
            {
                Abandon(chunk.MessageFirst, chunk.MessageLast);
            }
            else
            {
                _nextExpiry = Math.Min(_nextExpiry, chunk.ExpiresAt);
            }
        }
    }

    /// <summary>RFC 3758 section 3.5: the advanced peer ack point moves over abandoned chunks right after the cumulative TSN.</summary>
    private void AdvancePeerAckPoint()
    {
        if (!_forwardTsnSupported)
        {
            return;
        }

        while (SctpSerial.After(_nextTsn, _advancedAck + 1) && Chunk(_advancedAck + 1).State == SctpChunkState.Abandoned)
        {
            _advancedAck++;
        }

        if (SctpSerial.After(_advancedAck, _cumulative))
        {
            _forwardTsnDue = true;
        }
    }

    /// <summary>The last abandoned sequence number of each ordered stream up to the advanced peer ack point.</summary>
    private int CollectSkippedStreams()
    {
        int count = 0;

        for (uint tsn = _cumulative + 1; !SctpSerial.After(tsn, _advancedAck); tsn++)
        {
            ref var chunk = ref Chunk(tsn);

            if (chunk.State == SctpChunkState.Abandoned && (chunk.Flags & SctpFormat.UnorderedFlag) == 0)
            {
                count = Note(count, chunk.Stream, chunk.Sequence);
            }
        }

        return count;
    }

    private int Note(int count, ushort stream, ushort sequence)
    {
        for (int index = 0; index < count; index++)
        {
            if (_forwardStreams[index] == stream)
            {
                if (SctpSerial.After(sequence, _forwardSequences[index]))
                {
                    _forwardSequences[index] = sequence;
                }

                return count;
            }
        }

        if (count == MaxForwardStreams)
        {
            return count;
        }

        _forwardStreams[count] = stream;
        _forwardSequences[count] = sequence;

        return count + 1;
    }

    /// <summary>The fields every fragment of a message shares; an ordered message takes its stream's next sequence number.</summary>
    private SctpOutChunk Template(ushort stream, uint protocol, SctpSendPolicy policy, long now)
    {
        SctpOutChunk message = default;
        message.State = SctpChunkState.Queued;
        message.Stream = stream;
        message.Protocol = protocol;
        message.Reliability = policy.Reliability;
        message.Limit = policy.Limit;

        message.ExpiresAt = long.MaxValue;

        if (policy.Reliability == SctpReliability.Lifetime)
        {
            message.ExpiresAt = now + (policy.Limit * _ticksPerMillisecond);
            _nextExpiry = Math.Min(_nextExpiry, message.ExpiresAt);
        }

        if (!policy.Unordered)
        {
            message.Sequence = _sequences[stream];
            _sequences[stream]++;
        }

        return message;
    }

    private static byte FragmentFlags(int index, int fragments, bool unordered)
    {
        byte flags = 0;

        if (index == 0)
        {
            flags |= SctpFormat.BeginningFlag;
        }

        if (index == fragments - 1)
        {
            flags |= SctpFormat.EndFlag;
        }

        if (unordered)
        {
            flags |= SctpFormat.UnorderedFlag;
        }

        return flags;
    }

    /// <summary>
    /// Room for a message in the send buffer, which is freed in TSN order; a message never wraps, so when it does not
    /// fit before the end it starts over at the beginning.
    /// </summary>
    private bool TryReserve(int length, out int offset, out long start)
    {
        offset = (int)(_bufferTail % _buffer.Length);
        start = _bufferTail;

        if (offset + length > _buffer.Length)
        {
            start += _buffer.Length - offset;
            offset = 0;
        }

        if (start + length - _bufferHead > _buffer.Length)
        {
            return false;
        }

        _bufferTail = start + length;

        return true;
    }

    private ref SctpOutChunk Chunk(uint tsn)
    {
        return ref _chunks[tsn & _mask];
    }
}
