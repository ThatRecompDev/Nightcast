// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Phantom.Engine.Core.Collections;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Session;
using SessionCandidate = Phantom.Relay.Xbox.Session.IceCandidate;
using WebRtcCandidate = Phantom.Relay.WebRtc.Ice.IceCandidate;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// ICE, then DTLS, against the real console. Our host candidate goes through the service's ICE exchange, and the
/// console's candidates come from its answer and that exchange. The agent runs on a real UDP socket until it
/// connects. On the selected path our DTLS client (the answer says the console is passive) runs its handshake on its
/// own thread, fed by this loop. Afterwards the loop listens a little longer and counts what the console sends.
/// </summary>
public sealed class LiveIce : ILiveIce, IRtcpOutput, IDisposable
{
    private const int Dtls = 0;
    private const int Rtp = 1;
    private const int Other = 2;
    private const int CountKinds = 3;

    /// <summary>A DTLS record never exceeds this on a LAN path (the Ethernet payload less the IP and UDP headers).</summary>
    private const int DtlsLimit = 1200;

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private readonly IXHomeSessionService _sessions;
    private readonly PictureMailbox _mailbox;
    private readonly TimeSpan _listen;
    private readonly IDatagramTransport _transport;
    private readonly uint _hostAddress;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly ByteRing _ring;
    private readonly int[] _counts = new int[CountKinds];

    private DtlsDatagramBridge? _bridge;
    private ConsoleChannels? _channels;
    private MediaReceiver? _media;
    private DatagramEndpoint? _path;
    private long _channelsStartedAt;
    private TimeSpan? _firstRtp;

    /// <summary>
    /// Keeps the stream for <c>listen</c> once connected: a few seconds to measure, <see cref="TimeSpan.MaxValue"/> to
    /// watch until cancelled.
    /// </summary>
    public LiveIce(IXHomeSessionService sessions, IDatagramTransport transport, uint hostAddress, TimeProvider clock, IRandomSource random, PictureMailbox mailbox, TimeSpan listen)
    {
        _sessions = sessions;
        _mailbox = mailbox;
        _listen = listen;

        // The socket and the receive ring sized for its largest datagram.
        _transport = transport;
        _hostAddress = hostAddress;
        _clock = clock;
        _random = random;
        _ring = new ByteRing(ByteRing.CapacityFor(DatagramFrame.FrameLength(transport.MaxDatagramLength)));
    }

    public async Task<IceOutcome> RunAsync(StreamSession session, IceCredentials local, DtlsCertificate certificate, string answer, CancellationToken cancellation)
    {
        var remote = SessionIce.ReadCredentials(answer);

        if (remote is null)
        {
            return new IceOutcome(IceAgentState.Failed, null, TimeSpan.Zero, null, null, 0, 0, 0, 0);
        }

        var agent = new IceAgent(_transport, _clock, _random, local);
        var candidates = await CandidatesAsync(session, agent, answer, cancellation);
        long start = _clock.GetTimestamp();
        agent.Start(remote, candidates);

        Pump(agent, Deadline, () => agent.State != IceAgentState.Checking, cancellation);
        var elapsed = _clock.GetElapsedTime(start);
        var firstRoundTrip = agent.RoundTrip;
        var outcome = new IceOutcome(agent.State, agent.Selected, elapsed, firstRoundTrip, null, candidates.Count, 0, 0, 0);

        if (agent.State == IceAgentState.Connected)
        {
            outcome = RunDtls(agent, certificate, answer, outcome, cancellation);
            Pump(agent, _listen, static () => false, cancellation);
            outcome = outcome with { Channels = _channels?.Report(_firstRtp), Media = _media?.Report() };
        }

        return outcome with { LatestRoundTrip = agent.RoundTrip, DtlsDatagrams = _counts[Dtls], RtpDatagrams = _counts[Rtp], OtherDatagrams = _counts[Other] };
    }

    /// <summary>RTCP feedback goes straight out on the selected path, beside the DTLS records.</summary>
    public void Send(ReadOnlySpan<byte> packet)
    {
        if (_path is not null)
        {
            _transport.TrySend(_path.Value, packet);
        }
    }

    public void Dispose()
    {
        _bridge?.Dispose();
        _media?.Dispose();
    }

    /// <summary>Runs the handshake on its own thread while this loop carries its datagrams over the selected path.</summary>
    private IceOutcome RunDtls(IceAgent agent, DtlsCertificate certificate, string answer, IceOutcome outcome, CancellationToken cancellation)
    {
        string? expected = DtlsFingerprint.Read(answer);

        if (expected is null)
        {
            return outcome with { DtlsResult = "the answer carries no fingerprint" };
        }

        _bridge = new DtlsDatagramBridge(DtlsLimit);
        var client = new DtlsSrtpClient(new BcTlsCrypto(new SecureRandom()), certificate, expected);
        long start = _clock.GetTimestamp();
        var handshake = Task.Run(() => new DtlsClientProtocol().Connect(client, _bridge), cancellation);

        Pump(agent, Deadline, () => handshake.IsCompleted, cancellation);
        var elapsed = _clock.GetElapsedTime(start);

        if (!handshake.IsCompleted)
        {
            _bridge.Close();

            return outcome with { DtlsResult = "timed out", DtlsElapsed = elapsed };
        }

        if (handshake.IsFaulted)
        {
            string reason = handshake.Exception?.GetBaseException().Message ?? "unknown";

            return outcome with { DtlsResult = $"failed: {reason}", DtlsElapsed = elapsed };
        }

        // The session is up: the media path decrypts with the console's key, the data channels run over it.
        _path = agent.Selected;
        _media = new MediaReceiver(client.Profile!.Value, client.KeyingMaterial!, VideoPayloads.Of(answer), this, _mailbox, _clock);
        _channels = new ConsoleChannels(handshake.Result, _bridge, _clock, _random);
        _channels.Open();
        _channelsStartedAt = _clock.GetTimestamp();

        return outcome with { DtlsResult = "connected", DtlsElapsed = elapsed, SrtpProfile = client.Profile?.ToString(), CipherSuite = client.CipherSuite };
    }

    /// <summary>Sends our host candidate and gathers the console's from its answer and the exchange, without duplicates.</summary>
    private async Task<List<WebRtcCandidate>> CandidatesAsync(StreamSession session, IceAgent agent, string answer, CancellationToken cancellation)
    {
        var host = agent.HostCandidate(_hostAddress);
        SessionCandidate[] local = [new SessionCandidate(host.ToLine(), "0", 0)];
        var exchanged = await _sessions.ExchangeIceAsync(session, local, cancellation);
        var candidates = new List<WebRtcCandidate>(SessionIce.ReadCandidates(answer));

        foreach (var line in exchanged)
        {
            if (WebRtcCandidate.TryParse(line.Candidate, out var candidate) && !candidates.Exists(c => c.Endpoint == candidate!.Endpoint))
            {
                candidates.Add(candidate!);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Pumps without sleeping: a timed sleep on Windows lasts a whole ~15.6 ms timer tick. Yielding the thread reacts
    /// within microseconds, at the cost of one busy core for the probe's few seconds.
    /// </summary>
    private void Pump(IceAgent agent, TimeSpan length, Func<bool> done, CancellationToken cancellation)
    {
        long start = _clock.GetTimestamp();

        while (_clock.GetElapsedTime(start) < length && !done())
        {
            cancellation.ThrowIfCancellationRequested();
            PumpOnce(agent);
            Thread.Yield();
        }
    }

    private void PumpOnce(IceAgent agent)
    {
        agent.Service();
        SendDtls(agent);
        _transport.Receive(_ring);

        while (_ring.TryPeek(out var frame))
        {
            var payload = DatagramFrame.Payload(frame);

            if (!agent.TryHandle(DatagramFrame.Source(frame), payload))
            {
                Route(payload);
            }

            _ring.Release();
        }

        _channels?.Pump();
        _media?.Service();
        SendDtls(agent);
    }

    private void SendDtls(IceAgent agent)
    {
        var path = agent.Selected;

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
            _media?.Receive(payload);

            if (_firstRtp is null && _channels is not null)
            {
                _firstRtp = _clock.GetElapsedTime(_channelsStartedAt);
            }
        }
        else
        {
            _counts[Other]++;
        }
    }
}
