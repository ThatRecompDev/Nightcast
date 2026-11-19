// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Engine.Media.Vpx;
using Phantom.Relay.WebRtc.H264;
using Phantom.Relay.WebRtc.Rtp;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The live media path, measured: SRTP decrypted with the console's key, video RTP through the latency-first reorder
/// stage into the H.264 depacketizer, and every whole access unit decoded by the engine's H.264 decoder (openh264),
/// timed. Sound is counted, not decoded yet.
/// </summary>
public sealed class MediaReceiver : IAccessUnitSink, IDisposable
{
    private const int MaxSources = 8;
    private const int ReorderCapacity = 1024;
    private const int MaxPacket = 2048;
    private const int StreamWidth = 2560;
    private const int StreamHeight = 1440;
    private const int OpusPayload = 111;

    private static readonly TimeSpan ReorderWindow = TimeSpan.FromMilliseconds(3);

    private readonly SrtpContext _srtp;
    private readonly H264Depacketizer _depacketizer;
    private readonly RtpReorderBuffer _reorder;
    private readonly RtpHeader _header = new();
    private readonly HashSet<int> _videoPayloads;
    private readonly TimeProvider _clock;
    private readonly H264Decoder? _decoder;
    private readonly string? _decoderProblem;
    private readonly VideoFrame _picture = new();
    private readonly byte[] _scratch = new byte[MaxPacket];

    private long _decrypted;
    private long _refused;
    private long _rtcp;
    private long _audio;
    private long _units;
    private long _keyframes;
    private long _pictures;
    private long _decodeTicks;
    private long _worstTicks;

    public MediaReceiver(SrtpProfile profile, byte[] keyingMaterial, IReadOnlySet<int> videoPayloads, TimeProvider clock)
    {
        SrtpKeyingMaterial.Split(profile, keyingMaterial, localIsClient: true, out _, out var console);
        _srtp = SrtpContext.Create(profile, console, MaxSources);
        _depacketizer = new H264Depacketizer(this, H264Depacketizer.CapacityFor(StreamWidth, StreamHeight));
        _reorder = new RtpReorderBuffer(_depacketizer, clock, ReorderWindow, ReorderCapacity);
        _videoPayloads = [.. videoPayloads];
        _clock = clock;

        _decoder = H264Decoder.TryOpen(out _decoderProblem);
    }

    /// <summary>One RTP or RTCP datagram from the console.</summary>
    public void Receive(ReadOnlySpan<byte> datagram)
    {
        if (RtpFormat.IsRtcp(datagram))
        {
            _rtcp++;

            return;
        }

        if (datagram.Length > _scratch.Length)
        {
            _refused++;

            return;
        }

        var packet = _scratch.AsSpan(0, datagram.Length);
        datagram.CopyTo(packet);

        if (!_srtp.TryUnprotectRtp(packet, out int length) || !_header.TryRead(packet[..length]))
        {
            _refused++;

            return;
        }

        _decrypted++;
        Route(packet[..length]);
    }

    /// <summary>Lets the reorder stage give up on holes whose time has passed.</summary>
    public void Service()
    {
        _reorder.Service();
    }

    public void Deliver(ReadOnlySpan<byte> accessUnit, uint timestamp, H264AccessUnitContent content)
    {
        _units++;

        if ((content & H264AccessUnitContent.Idr) != 0)
        {
            _keyframes++;
        }

        if (_decoder is not null)
        {
            Decode(accessUnit, timestamp);
        }
    }

    public MediaReport Report()
    {
        var average = TimeSpan.Zero;

        if (_units > 0)
        {
            average = Duration(_decodeTicks / _units);
        }

        return new MediaReport(_decrypted, _refused, _rtcp, _audio, _units, _keyframes, _depacketizer.Dropped, _reorder.Lost, _pictures, _picture.Width, _picture.Height, average, Duration(_worstTicks), _decoderProblem);
    }

    public void Dispose()
    {
        _decoder?.Dispose();
    }

    private void Route(ReadOnlySpan<byte> packet)
    {
        if (_videoPayloads.Contains(_header.PayloadType))
        {
            _reorder.Push(packet);
        }
        else if (_header.PayloadType == OpusPayload)
        {
            _audio++;
        }
    }

    private void Decode(ReadOnlySpan<byte> accessUnit, uint timestamp)
    {
        long start = _clock.GetTimestamp();
        _decoder!.Decode(accessUnit, timestamp);

        while (_decoder.TakePicture(_picture))
        {
            _pictures++;
        }

        long spent = _clock.GetTimestamp() - start;
        _decodeTicks += spent;
        _worstTicks = Math.Max(_worstTicks, spent);
    }

    private TimeSpan Duration(long ticks)
    {
        return TimeSpan.FromSeconds((double)ticks / _clock.TimestampFrequency);
    }
}
