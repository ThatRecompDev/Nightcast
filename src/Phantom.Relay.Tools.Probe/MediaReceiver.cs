// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Vpx;
using Phantom.Relay.WebRtc.H264;
using Phantom.Relay.WebRtc.Rtp;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The live media path, measured. It decrypts SRTP with the console's key and sends video RTP through the
/// latency-first reorder stage into the H.264 depacketizer. Each whole access unit is handed to a decode worker on
/// its own thread, so this loop never waits on the decoder. After a loss, while the depacketizer waits for a
/// keyframe, it asks the console for one with an SRTCP PLI, at most once per <see cref="KeyframeRequestInterval"/>.
/// Sound is counted, not decoded yet.
/// </summary>
public sealed class MediaReceiver : IAccessUnitSink, IDisposable
{
    private const int MaxSources = 8;
    private const int ReorderCapacity = 1024;
    private const int MaxPacket = 2048;
    private const int StreamWidth = 2560;
    private const int StreamHeight = 1440;
    private const int OpusPayload = 111;

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
    private readonly DecodeWorker? _decoder;
    private readonly string? _decoderProblem;
    private readonly byte[] _scratch = new byte[MaxPacket];
    private readonly byte[] _feedback = new byte[MaxPacket];

    private uint _videoSsrc;
    private long _lastRequest;
    private long _requests;
    private long _decrypted;
    private long _refused;
    private long _rtcpPackets;
    private long _audio;
    private long _units;
    private long _keyframes;

    public MediaReceiver(SrtpProfile profile, byte[] keyingMaterial, IReadOnlySet<int> videoPayloads, IRtcpOutput rtcp, PictureMailbox mailbox, TimeProvider clock)
    {
        SrtpKeyingMaterial.Split(profile, keyingMaterial, localIsClient: true, out var own, out var console);
        _decrypt = SrtpContext.Create(profile, console, MaxSources);
        _protect = SrtpContext.Create(profile, own, MaxSources);
        _depacketizer = new H264Depacketizer(this, H264Depacketizer.CapacityFor(StreamWidth, StreamHeight));
        _reorder = new RtpReorderBuffer(_depacketizer, clock, ReorderWindow, ReorderCapacity);
        _videoPayloads = [.. videoPayloads];

        _rtcp = rtcp;
        _clock = clock;
        _decoder = OpenDecoder(mailbox, clock, out _decoderProblem);
        _decoder?.Start();
    }

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
        Route(packet[..length]);
    }

    /// <summary>Lets the reorder stage give up on holes whose time has passed, and asks for a keyframe when one is needed.</summary>
    public void Service()
    {
        _reorder.Service();
        RequestKeyframeWhenNeeded();
    }

    public void Deliver(ReadOnlySpan<byte> accessUnit, uint timestamp, H264AccessUnitContent content)
    {
        _units++;

        if ((content & H264AccessUnitContent.Idr) != 0)
        {
            _keyframes++;
        }

        _decoder?.Enqueue(accessUnit, timestamp);
    }

    public MediaReport Report()
    {
        var counts = new MediaCounts(_decrypted, _refused, _rtcpPackets, _audio, _units, _keyframes, _depacketizer.Dropped, _reorder.Lost, _requests);

        if (_decoder is null)
        {
            return new MediaReport(counts, 0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, _decoderProblem);
        }

        return new MediaReport(counts, _decoder.Pictures, _decoder.Width, _decoder.Height, _decoder.AverageDecode(), _decoder.WorstDecode(), _decoder.DeepestQueue, null);
    }

    public void Dispose()
    {
        _decoder?.Dispose();
    }

    private static DecodeWorker? OpenDecoder(PictureMailbox mailbox, TimeProvider clock, out string? problem)
    {
        var decoder = H264Decoder.TryOpen(out problem);

        if (decoder is null)
        {
            return null;
        }

        return new DecodeWorker(decoder, mailbox, clock);
    }

    private void Route(ReadOnlySpan<byte> packet)
    {
        if (_videoPayloads.Contains(_header.PayloadType))
        {
            _videoSsrc = _header.Ssrc;
            _reorder.Push(packet);
        }
        else if (_header.PayloadType == OpusPayload)
        {
            _audio++;
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
            _requests++;
            _lastRequest = now;
        }
    }
}
