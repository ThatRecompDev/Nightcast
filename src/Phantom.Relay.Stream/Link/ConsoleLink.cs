// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Phantom.Engine.Core.Collections;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.Stream.Channels;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Sdp;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.WebRtc.Srtp;
using SessionCandidate = Phantom.Relay.Xbox.Session.IceCandidate;
using WebRtcCandidate = Phantom.Relay.WebRtc.Ice.IceCandidate;

namespace Phantom.Relay.Stream.Link;

/// <summary>
/// The path to the console, pumped by one network thread: ICE on the transport, then our DTLS client (the answer says
/// the console is passive) running its handshake on a thread of its own, fed by the pump, then the data channels and
/// the media path over the session. Each datagram is told apart by its first byte (RFC 7983): STUN goes to the ICE
/// agent, DTLS to the handshake or the session, RTP and RTCP to the media path. It never waits: the caller pumps it
/// and decides how long each phase may take.
/// </summary>
public sealed class ConsoleLink : IRtcpOutput, IDisposable
{
    private const int Dtls = 0;
    private const int Rtp = 1;
    private const int Other = 2;
    private const int CountKinds = 3;

    /// <summary>A DTLS record never exceeds this on a LAN path (the Ethernet payload less the IP and UDP headers).</summary>
    private const int DtlsLimit = 1200;

    private readonly IDatagramTransport _transport;
    private readonly DecodeWorker _video;
    private readonly IStreamAudioSink _audio;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly IceAgent _agent;
    private readonly ByteRing _ring;
    private readonly int[] _counts = new int[CountKinds];

    private string _answer = string.Empty;
    private DtlsDatagramBridge? _bridge;
    private DtlsSrtpClient? _client;
    private Task<DtlsTransport>? _handshake;
    private DtlsTransport? _session;
    private long _handshakeStartedAt;
    private long _channelsStartedAt;
    private DatagramEndpoint? _path;

    public ConsoleLink(IDatagramTransport transport, IceCredentials local, DecodeWorker video, IStreamAudioSink audio, TimeProvider clock, IRandomSource random)
    {
        _transport = transport;
        _video = video;
        _audio = audio;
        _clock = clock;
        _random = random;

        // The ICE agent with the offer's credentials, and the receive ring sized for the largest datagram.
        _agent = new IceAgent(transport, clock, random, local);
        _ring = new ByteRing(ByteRing.CapacityFor(DatagramFrame.FrameLength(transport.MaxDatagramLength)));
    }

    public IceAgent Ice => _agent;

    /// <summary>How many candidates the console gave, through its answer and the ICE exchange.</summary>
    public int RemoteCandidates { get; private set; }

    /// <summary>How long the DTLS handshake took, once it ended.</summary>
    public TimeSpan? DtlsElapsed { get; private set; }

    public SrtpProfile? SrtpProfile => _client?.Profile;

    public int CipherSuite => _client?.CipherSuite ?? 0;

    /// <summary>The data channels, once DTLS connected.</summary>
    public ConsoleChannels? Channels { get; private set; }

    /// <summary>The media path, once DTLS connected.</summary>
    public MediaReceiver? Media { get; private set; }

    /// <summary>How long after the channels started the first RTP or RTCP datagram arrived.</summary>
    public TimeSpan? FirstRtpAfter { get; private set; }

    public int DtlsDatagrams => _counts[Dtls];

    public int RtpDatagrams => _counts[Rtp];

    public int OtherDatagrams => _counts[Other];

    /// <summary>Our host candidate at <paramref name="hostAddress"/>, as the service's ICE exchange carries it.</summary>
    public SessionCandidate HostCandidate(uint hostAddress)
    {
        return new SessionCandidate(_agent.HostCandidate(hostAddress).ToLine(), "0", 0);
    }

    /// <summary>
    /// Starts checking against the console's candidates from its answer and the ICE exchange, without duplicates; false
    /// when the answer carries no ICE credentials.
    /// </summary>
    public bool StartIce(string answer, IReadOnlyList<SessionCandidate> exchanged)
    {
        var remote = SessionIce.ReadCredentials(answer);

        if (remote is null)
        {
            return false;
        }

        var candidates = new List<WebRtcCandidate>(SessionIce.ReadCandidates(answer));

        foreach (var line in exchanged)
        {
            if (WebRtcCandidate.TryParse(line.Candidate, out var candidate) && !candidates.Exists(c => c.Endpoint == candidate!.Endpoint))
            {
                candidates.Add(candidate!);
            }
        }

        _answer = answer;
        RemoteCandidates = candidates.Count;
        _agent.Start(remote, candidates);

        return true;
    }

    /// <summary>Starts the handshake on its own thread; false when the answer carries no fingerprint to check.</summary>
    public bool StartDtls(DtlsCertificate certificate)
    {
        string? expected = DtlsFingerprint.Read(_answer);

        if (expected is null)
        {
            return false;
        }

        var bridge = new DtlsDatagramBridge(DtlsLimit);
        var client = new DtlsSrtpClient(new BcTlsCrypto(new SecureRandom()), certificate, expected);
        _bridge = bridge;
        _client = client;
        _handshakeStartedAt = _clock.GetTimestamp();
        _handshake = Task.Run(() => new DtlsClientProtocol().Connect(client, bridge));

        return true;
    }

    /// <summary>
    /// True once the handshake ended: then <paramref name="failure"/> is null and the channels and the media path are
    /// open, or it says why the handshake failed.
    /// </summary>
    public bool TryFinishDtls(out string? failure)
    {
        failure = null;
        var handshake = _handshake;

        if (handshake is null || !handshake.IsCompleted)
        {
            return false;
        }

        DtlsElapsed ??= _clock.GetElapsedTime(_handshakeStartedAt);

        if (!handshake.IsCompletedSuccessfully)
        {
            failure = handshake.Exception?.GetBaseException().Message ?? "cancelled";

            return true;
        }

        if (_session is null)
        {
            Open(handshake.Result);
        }

        return true;
    }

    /// <summary>
    /// One turn of the network loop: ICE's checks and keep-alives, every datagram that arrived routed, the channels and
    /// the media path serviced, and what DTLS wants sent sent.
    /// </summary>
    public void Pump()
    {
        _agent.Service();
        SendDtls();
        _transport.Receive(_ring);

        while (_ring.TryPeek(out var frame))
        {
            var payload = DatagramFrame.Payload(frame);

            if (!_agent.TryHandle(DatagramFrame.Source(frame), payload))
            {
                Route(payload);
            }

            _ring.Release();
        }

        Channels?.Pump();
        Media?.Service();
        SendDtls();
    }

    /// <summary>RTCP feedback goes straight out on the selected path, beside the DTLS records.</summary>
    public void Send(ReadOnlySpan<byte> packet)
    {
        if (_path is not null)
        {
            _transport.TrySend(_path.Value, packet);
        }
    }

    /// <summary>Ends the DTLS session with a close alert, sent at once, and stops a handshake still running.</summary>
    public void Dispose()
    {
        if (_session is not null)
        {
            CloseSession(_session);
        }

        _bridge?.Close();

        // A handshake still running reads the bridge until the close reaches it; it is disposed only when none is.
        if (_handshake is null || _handshake.IsCompleted)
        {
            _bridge?.Dispose();
        }
    }

    private void CloseSession(DtlsTransport session)
    {
        try
        {
            session.Close();
            SendDtls();
        }
        catch (IOException)
        {
            // The console is gone already: nothing is left to tell it.
        }
    }

    /// <summary>The session is up: the media path decrypts with the console's key, the data channels run over it.</summary>
    private void Open(DtlsTransport session)
    {
        _session = session;
        _path = _agent.Selected;
        Media = new MediaReceiver(_client!.Profile!.Value, _client.KeyingMaterial!, VideoPayloads.Of(_answer), this, _video, _audio, _clock);
        Channels = new ConsoleChannels(session, _bridge!, _clock, _random);
        Channels.Open();
        _channelsStartedAt = _clock.GetTimestamp();
    }

    private void SendDtls()
    {
        var path = _agent.Selected;

        if (_bridge is null || path is null)
        {
            return;
        }

        while (_bridge.TryTakeOutgoing(out var datagram))
        {
            _transport.TrySend(path.Value, datagram);
        }
    }

    private void Route(ReadOnlySpan<byte> payload)
    {
        if (DatagramClass.IsDtls(payload))
        {
            _counts[Dtls]++;
            _bridge?.Deliver(payload);
        }
        else if (DatagramClass.IsRtp(payload))
        {
            _counts[Rtp]++;
            Media?.Receive(payload);

            if (FirstRtpAfter is null && Channels is not null)
            {
                FirstRtpAfter = _clock.GetElapsedTime(_channelsStartedAt);
            }
        }
        else
        {
            _counts[Other]++;
        }
    }
}
