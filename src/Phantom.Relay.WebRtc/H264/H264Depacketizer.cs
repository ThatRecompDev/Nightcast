// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.H264;

/// <summary>
/// H.264 over RTP in packetization mode 1 (RFC 6184): single NAL unit packets, STAP-A and FU-A, rebuilt into Annex-B
/// access units (a start code before each NAL unit) in one buffer made up front. An access unit is handed to the sink
/// the moment it is complete: on the marker bit, or when a packet of the next timestamp shows the marker was not set.
/// <para>
/// Nothing corrupt reaches the decoder. A sequence gap breaks the access unit it falls in, and the one the next packet
/// belongs to, since the lost packets may be the tail of the one or the head of the other; so do an unfinished FU-A, a
/// malformed packet and a unit too large for the buffer. A broken unit is dropped and the stream needs a keyframe:
/// from then on only an intact unit with an IDR slice goes through, and <see cref="NeedsKeyframe"/> stays set, for a
/// PLI, until one does. The stream starts that way too, since nothing decodes before the first IDR.
/// </para>
/// </summary>
public sealed class H264Depacketizer : IRtpPacketSink
{
    private readonly IAccessUnitSink _sink;
    private readonly byte[] _buffer;
    private readonly RtpHeader _header = new();
    private int _length;
    private uint _timestamp;
    private bool _open;
    private bool _broken;
    private bool _inFragment;
    private bool _lossPending;
    private bool _started;
    private long _expected;
    private H264AccessUnitContent _content;

    public H264Depacketizer(IAccessUnitSink sink, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, H264Format.StartCodeLength);

        _sink = sink;
        _buffer = new byte[capacity];
    }

    /// <summary>True until an intact keyframe arrives, at the start and after every loss: the time to send a PLI.</summary>
    public bool NeedsKeyframe { get; private set; } = true;

    public long Delivered { get; private set; }

    /// <summary>Access units dropped, broken or waiting for a keyframe.</summary>
    public long Dropped { get; private set; }

    /// <summary>Sequence gaps seen.</summary>
    public long Gaps { get; private set; }

    /// <summary>A buffer that holds any conforming access unit of <paramref name="width"/> by <paramref name="height"/>.</summary>
    public static int CapacityFor(int width, int height)
    {
        return width * height * H264Format.RawBitsPerPixel / H264Format.BitsPerByte;
    }

    /// <summary>One RTP packet from the reorder stage, in sequence order.</summary>
    [HotPath]
    public void Deliver(ReadOnlySpan<byte> packet, long sequence)
    {
        if (!_header.TryRead(packet))
        {
            Lose();
            _started = true;
            _expected = sequence + 1;

            return;
        }

        Push(_header.Payload(packet), sequence, _header.Timestamp, _header.Marker);
    }

    /// <summary>One packet's payload with its 64-bit sequence number, RTP timestamp and marker bit.</summary>
    [HotPath]
    public void Push(ReadOnlySpan<byte> payload, long sequence, uint timestamp, bool marker)
    {
        if (_started && sequence != _expected)
        {
            Lose();
        }

        _started = true;
        _expected = sequence + 1;

        if (_open && timestamp != _timestamp)
        {
            Complete();
        }

        if (payload.IsEmpty)
        {
            CompleteOnMarker(marker);

            return;
        }

        Begin(timestamp);
        Append(payload);
        CompleteOnMarker(marker);
    }

    /// <summary>Data was lost: the open unit and the next one are broken, and the stream needs a keyframe.</summary>
    private void Lose()
    {
        Gaps++;
        NeedsKeyframe = true;
        _lossPending = true;
        _broken |= _open;
    }

    private void Begin(uint timestamp)
    {
        if (!_open)
        {
            _open = true;
            _timestamp = timestamp;
        }

        if (_lossPending)
        {
            _broken = true;
            _lossPending = false;
        }
    }

    private void CompleteOnMarker(bool marker)
    {
        if (marker && _open)
        {
            Complete();
        }
    }

    /// <summary>Hands the open unit on when it is intact and decodable, then starts afresh.</summary>
    private void Complete()
    {
        if (_inFragment || _broken)
        {
            NeedsKeyframe = true;
            Dropped++;
        }
        else if (_length > 0 && NeedsKeyframe && (_content & H264AccessUnitContent.Idr) == 0)
        {
            Dropped++;
        }
        else if (_length > 0)
        {
            NeedsKeyframe = false;
            Delivered++;
            _sink.Deliver(_buffer.AsSpan(0, _length), _timestamp, _content);
        }

        _open = false;
        _broken = false;
        _inFragment = false;
        _length = 0;
        _content = H264AccessUnitContent.None;
    }

    private void Append(ReadOnlySpan<byte> payload)
    {
        if (_broken)
        {
            return;
        }

        int type = payload[0] & H264Format.TypeMask;

        switch (type)
        {
            case H264Format.StapA:
                AppendAggregate(payload[1..]);

                return;
            case H264Format.FuA:
                AppendFragment(payload);

                return;
            default:
                AppendSingle(payload, type);

                return;
        }
    }

    private void AppendSingle(ReadOnlySpan<byte> nal, int type)
    {
        if (_inFragment || type < H264Format.NonIdrSlice || type > H264Format.LastSingleNalType)
        {
            _broken = true;

            return;
        }

        WriteNal(nal);
    }

    /// <summary>A STAP-A's NAL units, each after its 16-bit size.</summary>
    private void AppendAggregate(ReadOnlySpan<byte> units)
    {
        if (_inFragment || units.IsEmpty)
        {
            _broken = true;

            return;
        }

        while (!units.IsEmpty && !_broken)
        {
            int size = 0;

            if (units.Length >= H264Format.StapSizeLength)
            {
                size = BinaryPrimitives.ReadUInt16BigEndian(units);
            }

            if (size == 0 || size > units.Length - H264Format.StapSizeLength)
            {
                _broken = true;

                return;
            }

            WriteNal(units.Slice(H264Format.StapSizeLength, size));
            units = units[(H264Format.StapSizeLength + size)..];
        }
    }

    /// <summary>An FU-A: the start rebuilds the NAL unit header, every part appends its data, the end closes it.</summary>
    private void AppendFragment(ReadOnlySpan<byte> payload)
    {
        if (payload.Length <= H264Format.FuHeaderLength)
        {
            _broken = true;

            return;
        }

        int header = payload[1];
        bool start = (header & H264Format.FuStartBit) != 0;
        bool end = (header & H264Format.FuEndBit) != 0;
        var data = payload[H264Format.FuHeaderLength..];

        if (start == _inFragment || (start && end) || !Fits(H264Format.StartCodeLength + 1 + data.Length))
        {
            _broken = true;

            return;
        }

        if (start)
        {
            int type = header & H264Format.TypeMask;
            WriteStartCode();
            _buffer[_length] = (byte)((payload[0] & H264Format.IndicatorMask) | type);
            _length++;
            Track(type);
        }

        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
        _inFragment = !end;
    }

    private void WriteNal(ReadOnlySpan<byte> nal)
    {
        if ((nal[0] & H264Format.ForbiddenBit) != 0 || !Fits(H264Format.StartCodeLength + nal.Length))
        {
            _broken = true;

            return;
        }

        WriteStartCode();
        nal.CopyTo(_buffer.AsSpan(_length));
        _length += nal.Length;
        Track(nal[0] & H264Format.TypeMask);
    }

    private void WriteStartCode()
    {
        BinaryPrimitives.WriteUInt32BigEndian(_buffer.AsSpan(_length), H264Format.StartCode);
        _length += H264Format.StartCodeLength;
    }

    private bool Fits(int count)
    {
        return _length + count <= _buffer.Length;
    }

    private void Track(int type)
    {
        switch (type)
        {
            case H264Format.IdrSlice:
                _content |= H264AccessUnitContent.Idr;

                return;
            case H264Format.SequenceParameterSet:
                _content |= H264AccessUnitContent.Sps;

                return;
            case H264Format.PictureParameterSet:
                _content |= H264AccessUnitContent.Pps;

                return;
            default:
                return;
        }
    }
}
