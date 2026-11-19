// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.Stream.Sdp;
using Phantom.Relay.WebRtc.H264;
using Phantom.Relay.WebRtc.Rtp;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// The media path on the network thread. It decrypts SRTP with the console's key and sends video RTP through the
/// latency-first reorder stage into the H.264 depacketizer. Each whole access unit goes to the
/// <see cref="DecodeWorker"/>, so this loop never waits on the decoder. After a loss, while the depacketizer waits for
/// a keyframe, it asks the console for one with an SRTCP PLI, at most once per <see cref="KeyframeRequestInterval"/>.
/// Each Opus payload goes to the <see cref="IStreamAudioSink"/> as it arrives. The <see cref="FeedbackSender"/> sends the
/// bitrate feedback that is switched on, and the payload octets of video and sound are counted for their bitrates.
/// </summary>
public sealed class MediaReceiver : IAccessUnitSink
{
    private const int MaxSources = 8;
    private const int ReorderCapacity = 1024;
    private const int MaxPacket = 2048;
    private const int StreamWidth = 2560;
    private const int StreamHeight = 1440;

    /// <summary>Our RTCP sender id: any value; the console only needs it to be stable.</summary>
    private const uint FeedbackSsrc = 0x52454C59;

    private static readonly TimeSpan ReorderWindow = TimeSpan.FromMilliseconds(3);
    private static readonly TimeSpan KeyframeRequestInterval = TimeSpan.FromMilliseconds(100);

    private readonly SrtpContext _decrypt;
    private readonly SrtpContext _protect;
    private readonly H264Depacketizer _depacketizer;
    private readonly RtpReorderBuffer _reorder;
    private readonly RtpHeader _header = new();
    private readonly HashSet<int> _videoPayloads;
    private readonly TimeProvider _clock;
    private readonly IRtcpOutput _rtcp;
    private readonly DecodeWorker _video;
    private readonly IStreamAudioSink _audio;
    private readonly byte[] _scratch = new byte[MaxPacket];
    private readonly byte[] _feedback = new byte[MaxPacket];
    private readonly FeedbackSender _bitrate;

    private uint _videoSsrc;
    private long _lastRequest;
    private long _requests;
    private long _decrypted;
    private long _refused;
    private long _rtcpPackets;
    private long _audioPackets;
    private long _units;
    private long _keyframes;
    private long _videoBytes;
    private long _audioBytes;

    /// <summary>
    /// <paramref name="transportWideId"/> is the answer's id of the transport-wide sequence extension, 0 when it kept
    /// none; <paramref name="feedback"/> says which bitrate feedback goes out.
    /// </summary>
    public MediaReceiver(SrtpProfile profile, byte[] keyingMaterial, IReadOnlySet<int> videoPayloads, IRtcpOutput rtcp, DecodeWorker video, IStreamAudioSink audio, TimeProvider clock, BitrateFeedback feedback, int transportWideId)
    {
        SrtpKeyingMaterial.Split(profile, keyingMaterial, localIsClient: true, out var own, out var console);
        _decrypt = SrtpContext.Create(profile, console, MaxSources);
        _protect = SrtpContext.Create(profile, own, MaxSources);
        _depacketizer = new H264Depacketizer(this, H264Depacketizer.CapacityFor(StreamWidth, StreamHeight));
        _reorder = new RtpReorderBuffer(_depacketizer, clock, ReorderWindow, ReorderCapacity);
        _videoPayloads = [.. videoPayloads];

        _rtcp = rtcp;
        _video = video;
        _audio = audio;
        _clock = clock;
        _bitrate = new FeedbackSender(feedback, transportWideId, FeedbackSsrc, _protect, rtcp, clock);
    }

    /// <summary>Whole access units assembled (frames received).</summary>
    public long AccessUnits => Interlocked.Read(ref _units);

    /// <summary>Packets the reorder stage gave up on.</summary>
    public long Lost => _reorder.Lost;

    public long KeyframeRequests => Interlocked.Read(ref _requests);

    /// <summary>Video RTP payload octets received so far, read on the network thread.</summary>
    public long VideoBytes => _videoBytes;

    /// <summary>Sound RTP payload octets received so far, read on the network thread.</summary>
    public long AudioBytes => _audioBytes;

    /// <summary>One RTP or RTCP datagram from the console.</summary>
    public void Receive(ReadOnlySpan<byte> datagram)
    {
        if (RtpFormat.IsRtcp(datagram))
        {
            _rtcpPackets++;

            return;
        }

        if (datagram.Length > _scratch.Length)
        {
            _refused++;

            return;
        }

        var packet = _scratch.AsSpan(0, datagram.Length);
        datagram.CopyTo(packet);

        if (!_decrypt.TryUnprotectRtp(packet, out int length) || !_header.TryRead(packet[..length]))
        {
            _refused++;

            return;
        }

        _decrypted++;
        _bitrate.Record(packet[..length], _header);
        Route(packet[..length]);
    }

    /// <summary>
    /// Lets the reorder stage give up on holes whose time has passed, asks for a keyframe when one is needed, and sends
    /// the bitrate feedback that is due.
    /// </summary>
    public void Service()
    {
        _reorder.Service();
        RequestKeyframeWhenNeeded();
        _bitrate.Service(_videoSsrc);
    }

    public void Deliver(ReadOnlySpan<byte> accessUnit, uint timestamp, H264AccessUnitContent content)
    {
        Interlocked.Increment(ref _units);
        bool key = (content & H264AccessUnitContent.Idr) != 0;

        if (key)
        {
            _keyframes++;
        }

        _video.Enqueue(accessUnit, timestamp, key);
    }

    /// <summary>The network side's counts, read on the network thread.</summary>
    public MediaCounts Counts()
    {
        return new MediaCounts(_decrypted, _refused, _rtcpPackets, _audioPackets, _units, _keyframes, _depacketizer.Dropped, _reorder.Lost, _requests)
        {
            VideoBytes = _videoBytes,
            AudioBytes = _audioBytes,
            RembSent = _bitrate.RembSent,
            TransportFeedbackSent = _bitrate.TransportFeedbackSent,
        };
    }

    private void Route(ReadOnlySpan<byte> packet)
    {
        if (_videoPayloads.Contains(_header.PayloadType))
        {
            _videoSsrc = _header.Ssrc;
            _videoBytes += _header.PayloadLength;
            _reorder.Push(packet);
        }
        else if (_header.PayloadType == StreamOffer.OpusPayload)
        {
            _audioPackets++;
            _audioBytes += _header.PayloadLength;
            _audio.Push(_header.Payload(packet), _header.SequenceNumber, _header.Timestamp);
        }
    }

    /// <summary>A PLI (RFC 4585) for the video source, protected as SRTCP, when the depacketizer waits for a keyframe.</summary>
    private void RequestKeyframeWhenNeeded()
    {
        long now = _clock.GetTimestamp();

        if (!_depacketizer.NeedsKeyframe || _videoSsrc == 0 || _clock.GetElapsedTime(_lastRequest, now) < KeyframeRequestInterval)
        {
            return;
        }

        int length = RtcpWriter.WritePictureLossIndication(_feedback, FeedbackSsrc, _videoSsrc);

        if (_protect.TryProtectRtcp(_feedback, length, out int protectedLength))
        {
            _rtcp.Send(_feedback.AsSpan(0, protectedLength));
            Interlocked.Increment(ref _requests);
            _lastRequest = now;
        }
    }
}
