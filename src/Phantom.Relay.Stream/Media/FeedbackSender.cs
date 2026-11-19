// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.WebRtc.Rtp;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// The bitrate feedback the media path sends the console (<see cref="BitrateFeedback"/>), each packet protected as
/// SRTCP and sent alone beside the PLI (reduced-size RTCP, RFC 5506): a REMB with the switch's bitrate for the video's
/// SSRC as soon as that is known and once a <see cref="RembInterval"/> after; and, when the answer kept the
/// transport-wide sequence extension, the transport feedback of every packet that carried it, once a
/// <see cref="TransportInterval"/>. Arrival times are read from the injected clock as each datagram is handled on the
/// network thread. Off, it does nothing.
/// </summary>
public sealed class FeedbackSender
{
    /// <summary>Packets held between feedbacks: about a second at 80 Mbit/s, far more than one interval brings.</summary>
    private const int RingCapacity = 8192;

    /// <summary>A feedback stays within one LAN datagram with the SRTCP tag and index.</summary>
    private const int MaxFeedbackLength = 1200;

    private const int MaxPacket = 2048;

    /// <summary>When packets came faster than one feedback holds, a few go out in one turn.</summary>
    private const int MaxFeedbacksPerTurn = 4;

    private const ulong BitsPerKilobit = 1000;

    private static readonly TimeSpan RembInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan TransportInterval = TimeSpan.FromMilliseconds(50);

    private readonly uint _senderSsrc;
    private readonly ulong _rembBitsPerSecond;
    private readonly int _transportWideId;
    private readonly TransportFeedbackRecorder? _recorder;
    private readonly SrtpContext _protect;
    private readonly IRtcpOutput _rtcp;
    private readonly TimeProvider _clock;
    private readonly long _epoch;
    private readonly uint[] _rembSsrcs = new uint[1];
    private readonly byte[] _buffer = new byte[MaxPacket];

    private uint _mediaSsrc;
    private long _lastRemb;
    private long _lastTransport;

    /// <summary>
    /// Feedback from <paramref name="senderSsrc"/> as <paramref name="feedback"/> asks; <paramref name="transportWideId"/>
    /// is the answer's id of the transport-wide extension (0 when it did not keep it: then no transport feedback).
    /// </summary>
    public FeedbackSender(BitrateFeedback feedback, int transportWideId, uint senderSsrc, SrtpContext protect, IRtcpOutput rtcp, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        _senderSsrc = senderSsrc;
        _rembBitsPerSecond = (ulong)feedback.RembKbps * BitsPerKilobit;

        if (feedback.TransportWideCc && transportWideId > 0)
        {
            _transportWideId = transportWideId;
            _recorder = new TransportFeedbackRecorder(RingCapacity);
        }

        _protect = protect;
        _rtcp = rtcp;
        _clock = clock;
        _epoch = clock.GetTimestamp();
        _lastTransport = _epoch;
    }

    public long RembSent { get; private set; }

    public long TransportFeedbackSent { get; private set; }

    /// <summary>Notes the arrival of a decrypted packet that carries the transport-wide sequence number.</summary>
    [HotPath]
    public void Record(ReadOnlySpan<byte> packet, RtpHeader header)
    {
        if (_recorder is null || !RtpHeaderExtensions.TryFind(packet, header, _transportWideId, out var value) || value.Length < TransportFeedbackFormat.ExtensionLength)
        {
            return;
        }

        _recorder.Record(BinaryPrimitives.ReadUInt16BigEndian(value), _clock.GetElapsedTime(_epoch));
        _mediaSsrc = header.Ssrc;
    }

    /// <summary>Sends what is due: the REMB for <paramref name="videoSsrc"/> (0 until known), the transport feedback.</summary>
    public void Service(uint videoSsrc)
    {
        long now = _clock.GetTimestamp();
        SendRembWhenDue(videoSsrc, now);
        SendTransportFeedbackWhenDue(now);
    }

    private void SendRembWhenDue(uint videoSsrc, long now)
    {
        if (_rembBitsPerSecond == 0 || videoSsrc == 0 || (RembSent > 0 && _clock.GetElapsedTime(_lastRemb, now) < RembInterval))
        {
            return;
        }

        _rembSsrcs[0] = videoSsrc;
        int length = RtcpWriter.WriteReceiverEstimatedMaxBitrate(_buffer, _senderSsrc, _rembBitsPerSecond, _rembSsrcs);

        if (Send(length))
        {
            RembSent++;
            _lastRemb = now;
        }
    }

    private void SendTransportFeedbackWhenDue(long now)
    {
        if (_recorder is null || !_recorder.HasPending || _clock.GetElapsedTime(_lastTransport, now) < TransportInterval)
        {
            return;
        }

        _lastTransport = now;

        for (int i = 0; i < MaxFeedbacksPerTurn && _recorder.HasPending; i++)
        {
            int length = _recorder.Write(_buffer.AsSpan(0, MaxFeedbackLength), _senderSsrc, _mediaSsrc);

            if (length == 0 || !Send(length))
            {
                return;
            }

            TransportFeedbackSent++;
        }
    }

    private bool Send(int length)
    {
        if (!_protect.TryProtectRtcp(_buffer, length, out int protectedLength))
        {
            return false;
        }

        _rtcp.Send(_buffer.AsSpan(0, protectedLength));

        return true;
    }
}
