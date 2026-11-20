// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Randomness;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// One SCTP association as WebRTC runs it over DTLS (RFC 9260, RFC 8261, RFC 3758): port 5000 at both ends, no IP
/// addresses, a pure state machine over <see cref="ISctpOutput"/>. <see cref="Connect"/> starts it (this side sends
/// INIT); a side that did not connect answers the peer's INIT. Then <see cref="Service"/> runs the timers every frame,
/// <see cref="Receive"/> takes every packet that arrives, <see cref="Send"/> queues a message and sends what the
/// windows allow at once, and <see cref="TryPeek"/> and <see cref="Release"/> read whole messages in order. Nothing is
/// allocated after construction.
/// </summary>
public sealed class SctpAssociation
{
    /// <summary>
    /// This side's state cookie: random bytes it keeps and compares. The association itself holds the state the
    /// cookie stands for, so the cookie needs no signature; DTLS already authenticates the peer.
    /// </summary>
    private const int CookieLength = 16;

    /// <summary>The most packets one call sends; the rest waits for the next <see cref="Service"/>.</summary>
    private const int MaxPacketsPerFlush = 256;

    private const long MillisecondsPerSecond = 1000;

    private readonly ISctpOutput _output;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly SctpOptions _options;
    private readonly SctpPacketBuilder _builder;
    private readonly SctpRto _rto;
    private readonly SctpSender _sender;
    private readonly SctpReceiver _receiver;
    private readonly byte[] _localCookie = new byte[CookieLength];
    private readonly byte[] _peerCookie;

    private int _peerCookieLength;
    private bool _cookieIssued;
    private uint _localTag;
    private uint _peerTag;
    private uint _localInitialTsn;
    private SctpInitParameters _peer;
    private long? _controlAt;
    private int _controlCount;

    public SctpAssociation(ISctpOutput output, TimeProvider clock, IRandomSource random, SctpOptions options)
    {
        options.Validate();
        _output = output;
        _clock = clock;
        _random = random;
        _options = options;
        _builder = new SctpPacketBuilder(options.MaxPacketLength);

        // The halves and the timeout they share, all sized once.
        _peerCookie = new byte[options.MaxPacketLength];
        _rto = new SctpRto(Ticks(SctpTiming.InitialRto), Ticks(SctpTiming.MinRto), Ticks(SctpTiming.MaxRto));
        _sender = new SctpSender(options, _rto, clock.TimestampFrequency / MillisecondsPerSecond);
        _receiver = new SctpReceiver(options, Ticks(SctpTiming.DelayedAck));
    }

    public SctpAssociationState State { get; private set; }

    public SctpCloseReason CloseReason { get; private set; }

    /// <summary>The first error cause code of the peer's ABORT (RFC 9260 section 3.3.10); 0 when it gave none or did not abort.</summary>
    public ushort PeerAbortCause { get; private set; }

    /// <summary>Data retransmission timeouts in a row; a high count before a close means the peer stopped acknowledging.</summary>
    public int TimeoutsInARow => _sender.ErrorCount;

    /// <summary>True when the peer announced FORWARD TSN, so partially reliable messages can be given up.</summary>
    public bool PeerSupportsPartialReliability => _peer.ForwardTsn;

    /// <summary>Streams this side announces both ways; stream identifiers lie below it.</summary>
    public int Streams => _options.Streams;

    /// <summary>Streams this side may send on: the fewer of its own and the peer's inbound streams.</summary>
    public int OutboundStreams => _sender.OutboundStreams;

    /// <summary>Bytes of messages queued or waiting for an acknowledgement.</summary>
    public long BufferedAmount => _sender.BufferedBytes;

    /// <summary>The current congestion window, for statistics.</summary>
    public int CongestionWindow => _sender.CongestionWindow;

    /// <summary>Starts the association: sends INIT and retries it until the peer answers.</summary>
    public void Connect()
    {
        if (State != SctpAssociationState.Closed || CloseReason != SctpCloseReason.None || _cookieIssued)
        {
            throw new InvalidOperationException("an association connects once, from Closed");
        }

        ChooseLocal();
        State = SctpAssociationState.CookieWait;
        SendInit(SctpFormat.Init, 0, ReadOnlySpan<byte>.Empty);
        ArmControl(_clock.GetTimestamp());
    }

    /// <summary>Runs the timers (handshake, shutdown, retransmission, delayed SACK) and sends what is due.</summary>
    [HotPath]
    public void Service()
    {
        long now = _clock.GetTimestamp();
        ServiceControlTimer(now);

        if (!CarriesData())
        {
            return;
        }

        if (!_sender.Expire(now))
        {
            Fail(SctpCloseReason.TimedOut);

            return;
        }

        Pump(now);
    }

    /// <summary>Takes one packet that arrived: checks its checksum, ports and tag, then handles its chunks in order.</summary>
    [HotPath]
    public void Receive(ReadOnlySpan<byte> packet)
    {
        if (!SctpPacket.HasValidChecksum(packet)
            || SctpPacket.SourcePort(packet) != SctpFormat.WebRtcPort
            || SctpPacket.DestinationPort(packet) != SctpFormat.WebRtcPort)
        {
            return;
        }

        long now = _clock.GetTimestamp();
        uint tag = SctpPacket.VerificationTag(packet);
        int offset = SctpFormat.CommonHeaderLength;

        while (SctpPacket.TryReadChunk(packet, ref offset, out byte type, out byte flags, out var value))
        {
            if (!AcceptsTag(type, flags, tag) || !Handle(type, flags, value, now))
            {
                break;
            }
        }

        _receiver.EndPacket(now);
        Pump(now);
    }

    /// <summary>
    /// Queues a message on <paramref name="stream"/> and sends what the windows allow at once; false when the
    /// association is not established, the stream is not open both ways, the message is empty or the send buffer full.
    /// </summary>
    [HotPath]
    public bool Send(ushort stream, uint protocol, ReadOnlySpan<byte> payload, SctpSendPolicy policy)
    {
        if (State != SctpAssociationState.Established)
        {
            return false;
        }

        long now = _clock.GetTimestamp();

        if (!_sender.Enqueue(stream, protocol, payload, policy, now))
        {
            return false;
        }

        Flush(now);

        return true;
    }

    /// <summary>The oldest whole message received, which stays readable until <see cref="Release"/>.</summary>
    [HotPath]
    public bool TryPeek(out ushort stream, out uint protocol, out ReadOnlySpan<byte> payload)
    {
        return _receiver.TryPeek(out stream, out protocol, out payload);
    }

    [HotPath]
    public void Release()
    {
        _receiver.Release();
    }

    /// <summary>Starts a graceful shutdown: what is queued still goes out and is acknowledged, then both sides close.</summary>
    public void Shutdown()
    {
        if (State == SctpAssociationState.Established)
        {
            State = SctpAssociationState.ShutdownPending;
            Pump(_clock.GetTimestamp());
        }
    }

    /// <summary>Ends the association at once, telling the peer with ABORT; whatever is queued is dropped.</summary>
    public void Abort()
    {
        if (State != SctpAssociationState.Closed)
        {
            Fail(SctpCloseReason.Aborted);
        }
    }

    [HotPath]
    private bool Handle(byte type, byte flags, ReadOnlySpan<byte> value, long now)
    {
        switch (type)
        {
            case SctpFormat.Data:
                return OnData(flags, value);
            case SctpFormat.Init:
                return OnInit(value);
            case SctpFormat.InitAck:
                return OnInitAck(value, now);
            case SctpFormat.Sack:
                return OnSack(value, now);
            case SctpFormat.Heartbeat:
                return OnHeartbeat(value);
            case SctpFormat.Abort:
                return OnAbort(value);
            case SctpFormat.Shutdown:
                return OnShutdown(value, now);
            case SctpFormat.ShutdownAck:
                return OnShutdownAck();
            case SctpFormat.CookieEcho:
                return OnCookieEcho(value);
            case SctpFormat.CookieAck:
                return OnCookieAck();
            case SctpFormat.ShutdownComplete:
                return OnShutdownComplete();
            case SctpFormat.ForwardTsn:
                return OnForwardTsn(value);
            case SctpFormat.HeartbeatAck:
            case SctpFormat.Error:
                return true;
            default:
                return (type & SctpFormat.SkipUnknownBit) != 0;
        }
    }

    /// <summary>
    /// RFC 9260 section 8.5: INIT carries tag zero; ABORT and SHUTDOWN COMPLETE with the T bit carry the tag this side
    /// sends; everything else carries this side's own tag.
    /// </summary>
    private bool AcceptsTag(byte type, byte flags, uint tag)
    {
        if (type == SctpFormat.Init)
        {
            return tag == 0;
        }

        if ((type == SctpFormat.Abort || type == SctpFormat.ShutdownComplete) && (flags & SctpFormat.TagReflectedFlag) != 0)
        {
            return _peerTag != 0 && tag == _peerTag;
        }

        return _localTag != 0 && tag == _localTag;
    }

    private bool OnData(byte flags, ReadOnlySpan<byte> value)
    {
        if (CarriesData())
        {
            _receiver.OnData(flags, value);
        }

        return true;
    }

    /// <summary>The peer starts the association: answer with INIT ACK and a cookie, keeping no more than one tag and cookie.</summary>
    private bool OnInit(ReadOnlySpan<byte> value)
    {
        if (State != SctpAssociationState.Closed || CloseReason != SctpCloseReason.None || !SctpInitChunk.TryRead(value, out var init, out _))
        {
            return false;
        }

        if (!_cookieIssued)
        {
            ChooseLocal();
            FillRandom(_localCookie);
            _cookieIssued = true;
        }

        _peer = init;
        _peerTag = init.InitiateTag;
        SendInit(SctpFormat.InitAck, init.InitiateTag, _localCookie);

        return false;
    }

    private bool OnInitAck(ReadOnlySpan<byte> value, long now)
    {
        if (State != SctpAssociationState.CookieWait || !SctpInitChunk.TryRead(value, out var init, out var cookie))
        {
            return false;
        }

        if (cookie.IsEmpty || cookie.Length + SctpFormat.CommonHeaderLength + SctpFormat.ChunkHeaderLength > _peerCookie.Length)
        {
            return false;
        }

        _peer = init;
        _peerTag = init.InitiateTag;
        cookie.CopyTo(_peerCookie);
        _peerCookieLength = cookie.Length;
        State = SctpAssociationState.CookieEchoed;
        SendCookieEcho();

        ArmControl(now);

        return false;
    }

    private bool OnCookieEcho(ReadOnlySpan<byte> value)
    {
        if (!_cookieIssued || CloseReason != SctpCloseReason.None || !value.SequenceEqual(_localCookie))
        {
            return true;
        }

        if (State == SctpAssociationState.Closed)
        {
            Establish();
        }

        SendEmpty(SctpFormat.CookieAck, 0);

        return true;
    }

    private bool OnCookieAck()
    {
        if (State == SctpAssociationState.CookieEchoed)
        {
            _controlAt = null;
            Establish();
        }

        return true;
    }

    private bool OnSack(ReadOnlySpan<byte> value, long now)
    {
        if (CarriesData())
        {
            _sender.OnSack(value, now);
        }

        return true;
    }

    private bool OnForwardTsn(ReadOnlySpan<byte> value)
    {
        if (CarriesData())
        {
            _receiver.OnForwardTsn(value);
        }

        return true;
    }

    /// <summary>Echoes the heartbeat's information back unchanged.</summary>
    private bool OnHeartbeat(ReadOnlySpan<byte> value)
    {
        _builder.Begin(SctpFormat.WebRtcPort, SctpFormat.WebRtcPort, _peerTag);

        if (_peerTag != 0 && _builder.Fits(value.Length))
        {
            value.CopyTo(_builder.AddChunk(SctpFormat.HeartbeatAck, 0, value.Length));
            _output.Send(_builder.Seal());
        }

        return true;
    }

    /// <summary>The peer aborts: its first error cause (RFC 9260 section 3.3.10) is kept for the report.</summary>
    private bool OnAbort(ReadOnlySpan<byte> value)
    {
        if (value.Length >= sizeof(ushort))
        {
            PeerAbortCause = BinaryPrimitives.ReadUInt16BigEndian(value);
        }

        Close(SctpCloseReason.AbortedByPeer);

        return false;
    }

    /// <summary>The peer shuts down: its cumulative TSN acknowledges, and the SHUTDOWN ACK waits until this side's data is through.</summary>
    private bool OnShutdown(ReadOnlySpan<byte> value, long now)
    {
        if (!CarriesData() || value.Length < SctpFormat.ShutdownLength)
        {
            return true;
        }

        _sender.OnCumulativeAck(BinaryPrimitives.ReadUInt32BigEndian(value), now);

        if (State is SctpAssociationState.Established or SctpAssociationState.ShutdownPending)
        {
            State = SctpAssociationState.ShutdownReceived;
        }
        else if (State == SctpAssociationState.ShutdownSent)
        {
            State = SctpAssociationState.ShutdownAckSent;
            SendEmpty(SctpFormat.ShutdownAck, 0);
            ArmControl(now);
        }

        return true;
    }

    private bool OnShutdownAck()
    {
        if (State is SctpAssociationState.ShutdownSent or SctpAssociationState.ShutdownAckSent)
        {
            SendEmpty(SctpFormat.ShutdownComplete, 0);
            Close(SctpCloseReason.Shutdown);
        }

        return false;
    }

    private bool OnShutdownComplete()
    {
        if (State == SctpAssociationState.ShutdownAckSent)
        {
            Close(SctpCloseReason.Shutdown);
        }

        return false;
    }

    /// <summary>Delivers whole messages, sends what is due and moves a shutdown on once everything sent is acknowledged.</summary>
    [HotPath]
    private void Pump(long now)
    {
        if (!CarriesData())
        {
            return;
        }

        _receiver.Deliver();
        Flush(now);

        if (!_sender.IsIdle)
        {
            return;
        }

        if (State == SctpAssociationState.ShutdownPending)
        {
            State = SctpAssociationState.ShutdownSent;
            SendShutdown();
            ArmControl(now);
        }
        else if (State == SctpAssociationState.ShutdownReceived)
        {
            State = SctpAssociationState.ShutdownAckSent;
            SendEmpty(SctpFormat.ShutdownAck, 0);
            ArmControl(now);
        }
    }

    /// <summary>Sends packets of a SACK when one is due, a FORWARD TSN when one is due and DATA, while there is more.</summary>
    [HotPath]
    private void Flush(long now)
    {
        for (int packet = 0; packet < MaxPacketsPerFlush; packet++)
        {
            _builder.Begin(SctpFormat.WebRtcPort, SctpFormat.WebRtcPort, _peerTag);

            if (_receiver.SackDue(now))
            {
                _receiver.WriteSack(_builder);
            }

            _sender.WriteForwardTsn(_builder, now);
            bool full = _sender.WriteData(_builder, now);

            if (!_builder.HasChunks)
            {
                return;
            }

            _output.Send(_builder.Seal());

            if (!full)
            {
                return;
            }
        }
    }

    /// <summary>T1 (INIT, COOKIE ECHO) and T2 (SHUTDOWN, SHUTDOWN ACK): resends with a doubled timeout, then gives up.</summary>
    [HotPath]
    private void ServiceControlTimer(long now)
    {
        if (!_controlAt.HasValue || now < _controlAt.Value)
        {
            return;
        }

        _controlCount++;
        int limit = State is SctpAssociationState.CookieWait or SctpAssociationState.CookieEchoed ? SctpTiming.MaxInitRetransmits : SctpTiming.MaxAssociationRetransmits;

        if (_controlCount > limit)
        {
            Fail(SctpCloseReason.TimedOut);

            return;
        }

        _rto.Backoff();
        _controlAt = now + _rto.Current;
        ResendControl();
    }

    private void ResendControl()
    {
        switch (State)
        {
            case SctpAssociationState.CookieWait:
                SendInit(SctpFormat.Init, 0, ReadOnlySpan<byte>.Empty);
                break;
            case SctpAssociationState.CookieEchoed:
                SendCookieEcho();
                break;
            case SctpAssociationState.ShutdownSent:
                SendShutdown();
                break;
            case SctpAssociationState.ShutdownAckSent:
                SendEmpty(SctpFormat.ShutdownAck, 0);
                break;
            default:
                _controlAt = null;
                break;
        }
    }

    private void ArmControl(long now)
    {
        _controlCount = 0;
        _controlAt = now + _rto.Current;
    }

    private void Establish()
    {
        int outbound = Math.Min(_options.Streams, (int)_peer.InboundStreams);
        _sender.Start(_localInitialTsn, _peer.Window, _peer.ForwardTsn, outbound);
        _receiver.Start(_peer.InitialTsn);
        State = SctpAssociationState.Established;
    }

    /// <summary>Ends the association from this side, telling the peer with ABORT when there is one.</summary>
    private void Fail(SctpCloseReason reason)
    {
        if (_peerTag != 0)
        {
            SendEmpty(SctpFormat.Abort, 0);
        }

        Close(reason);
    }

    private void Close(SctpCloseReason reason)
    {
        State = SctpAssociationState.Closed;
        CloseReason = reason;
        _controlAt = null;
    }

    private void SendInit(byte type, uint tag, ReadOnlySpan<byte> cookie)
    {
        var streams = (ushort)_options.Streams;
        var init = new SctpInitParameters(_localTag, _receiver.Window, streams, streams, _localInitialTsn, true);
        _builder.Begin(SctpFormat.WebRtcPort, SctpFormat.WebRtcPort, tag);
        SctpInitChunk.Write(_builder.AddChunk(type, 0, SctpInitChunk.Length(cookie.Length)), init, cookie);
        _output.Send(_builder.Seal());
    }

    private void SendCookieEcho()
    {
        _builder.Begin(SctpFormat.WebRtcPort, SctpFormat.WebRtcPort, _peerTag);
        _peerCookie.AsSpan(0, _peerCookieLength).CopyTo(_builder.AddChunk(SctpFormat.CookieEcho, 0, _peerCookieLength));
        _output.Send(_builder.Seal());
    }

    private void SendShutdown()
    {
        _builder.Begin(SctpFormat.WebRtcPort, SctpFormat.WebRtcPort, _peerTag);
        BinaryPrimitives.WriteUInt32BigEndian(_builder.AddChunk(SctpFormat.Shutdown, 0, SctpFormat.ShutdownLength), _receiver.Cumulative);
        _output.Send(_builder.Seal());
    }

    private void SendEmpty(byte type, byte flags)
    {
        _builder.Begin(SctpFormat.WebRtcPort, SctpFormat.WebRtcPort, _peerTag);
        _builder.AddChunk(type, flags, 0);
        _output.Send(_builder.Seal());
    }

    /// <summary>This side's verification tag (never zero) and first TSN, drawn from the injected source.</summary>
    private void ChooseLocal()
    {
        do
        {
            _localTag = _random.NextUInt32();
        }
        while (_localTag == 0);

        _localInitialTsn = _random.NextUInt32();
    }

    private void FillRandom(Span<byte> destination)
    {
        for (int offset = 0; offset < destination.Length; offset += sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], _random.NextUInt32());
        }
    }

    private bool CarriesData()
    {
        return State is SctpAssociationState.Established
            or SctpAssociationState.ShutdownPending
            or SctpAssociationState.ShutdownSent
            or SctpAssociationState.ShutdownReceived;
    }

    private long Ticks(TimeSpan span)
    {
        return span.Ticks * _clock.TimestampFrequency / TimeSpan.TicksPerSecond;
    }
}
