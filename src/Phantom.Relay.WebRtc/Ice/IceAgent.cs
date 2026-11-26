// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Stun;
using Phantom.Relay.WebRtc.Turn;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// A controlling ICE agent (RFC 8445) with one host candidate, optionally a TURN relayed one, and one component, as a
/// pure state machine over a datagram transport: start it with the remote side's credentials and candidates, then
/// <see cref="Service"/> every frame and hand every received datagram to it (one the relay passed on to
/// <see cref="TryHandleRelayed"/>). Checks go out best pair first, paced, each nominating its pair (USE-CANDIDATE), so
/// the first pair that answers is selected at once: the fastest path to a connection. The remote side's checks are
/// answered and trigger a check back, from a spare pair when they come from an address no candidate named (the console
/// behind its NAT, away from home). Nothing is allocated once checking started.
/// </summary>
public sealed class IceAgent
{
    private const int WordBits = 32;

    /// <summary>
    /// How many peer-reflexive pairs the agent learns from checks: the console's NAT shows it under one address, a
    /// few more cover a NAT that maps each destination apart.
    /// </summary>
    private const int MaxLearned = 4;

    /// <summary>Each remote candidate is paired with our host candidate and, with a relay, our relayed one.</summary>
    private const int PathsPerCandidate = 2;

    /// <summary>Our relayed candidate's priority: the lowest type preference, so relayed pairs come last.</summary>
    private static readonly uint RelayPriority = IcePriorities.Candidate(IcePriorities.RelayedPreference, IcePriorities.SingleLocalPreference, IcePriorities.RtpComponent);

    private readonly IDatagramTransport _transport;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly StunIntegrity _localIntegrity;
    private readonly ulong _tieBreaker;
    private readonly uint _checkPriority;
    private readonly uint _hostPriority;
    private readonly byte[] _send = new byte[StunFormat.MaxMessageLength];
    private readonly List<IcePair> _pairs = [];
    private readonly IcePair[] _spares = new IcePair[MaxLearned];

    private int _learned;
    private TurnClient? _relay;
    private long _relayFrom;
    private StunIntegrity? _remoteIntegrity;
    private byte[] _outgoingUsername = [];
    private byte[] _incomingUsername = [];
    private IcePair? _selected;
    private long _nextCheckAt;
    private long _keepAliveAt;

    public IceAgent(IDatagramTransport transport, TimeProvider clock, IRandomSource random, IceCredentials local)
    {
        _transport = transport;
        _clock = clock;
        _random = random;
        Local = local;

        // This agent's signature, its role tie-breaker and the priority its checks announce.
        _localIntegrity = StunIntegrity.ForPassword(local.Password);
        _tieBreaker = ((ulong)random.NextUInt32() << WordBits) | random.NextUInt32();
        _checkPriority = IcePriorities.Candidate(IcePriorities.PeerReflexivePreference, IcePriorities.SingleLocalPreference, IcePriorities.RtpComponent);
        _hostPriority = IcePriorities.Candidate(IcePriorities.HostPreference, IcePriorities.SingleLocalPreference, IcePriorities.RtpComponent);

        // The pairs a check from an unnamed address can claim, made now so learning one allocates nothing.
        for (int index = 0; index < _spares.Length; index++)
        {
            _spares[index] = new IcePair();
        }
    }

    public IceCredentials Local { get; }

    public IceAgentState State { get; private set; }

    /// <summary>The remote endpoint of the selected pair, once <see cref="State"/> is <see cref="IceAgentState.Connected"/>.</summary>
    public DatagramEndpoint? Selected => _selected?.Endpoint;

    /// <summary>What the selected remote candidate is: a host one at home, a reflexive one across the internet.</summary>
    public IceCandidateKind? SelectedKind => _selected?.Kind;

    /// <summary>True when the selected path runs through the TURN relay: datagrams on it go through <see cref="Send"/>.</summary>
    public bool SelectedRelayed => _selected?.Relayed == true;

    public IReadOnlyList<IcePair> Pairs => _pairs;

    /// <summary>
    /// The selected path's latest round trip, renewed by every keep-alive: the network part of the stream's latency.
    /// </summary>
    public TimeSpan? RoundTrip => _selected?.RoundTrip;

    /// <summary>This agent's host candidate for the signaling channel, at <paramref name="address"/> (the machine's own).</summary>
    public IceCandidate HostCandidate(uint address)
    {
        uint priority = IcePriorities.Candidate(IcePriorities.HostPreference, IcePriorities.SingleLocalPreference, IcePriorities.RtpComponent);

        return new IceCandidate("1", IcePriorities.RtpComponent, priority, new DatagramEndpoint(address, _transport.LocalEndpoint.Port), IceCandidateKind.Host);
    }

    /// <summary>
    /// Our relayed candidate on <paramref name="relay"/>'s allocation, for the signaling channel; null until allocated.
    /// </summary>
    public static IceCandidate? RelayCandidate(TurnClient relay)
    {
        var relayed = relay.Relayed;

        if (relayed is null)
        {
            return null;
        }

        return new IceCandidate("3", IcePriorities.RtpComponent, RelayPriority, relayed.Value, IceCandidateKind.Relayed);
    }

    /// <summary>
    /// Our server-reflexive candidate (our NAT's mapping, as the relay's server saw it), for the signaling channel; it
    /// shares the host candidate's socket, so it adds no pairs. Null until allocated.
    /// </summary>
    public static IceCandidate? ReflexiveCandidate(TurnClient relay)
    {
        var mapped = relay.Mapped;

        if (mapped is null)
        {
            return null;
        }

        uint priority = IcePriorities.Candidate(IcePriorities.ServerReflexivePreference, IcePriorities.SingleLocalPreference, IcePriorities.RtpComponent);

        return new IceCandidate("2", IcePriorities.RtpComponent, priority, mapped.Value, IceCandidateKind.ServerReflexive);
    }

    /// <summary>Pairs every remote RTP-component candidate with the host candidate, best first, and starts checking.</summary>
    public void Start(IceCredentials remote, IReadOnlyList<IceCandidate> candidates)
    {
        Start(remote, candidates, null);
    }

    /// <summary>
    /// As <see cref="Start(IceCredentials, IReadOnlyList{IceCandidate})"/>, and, when <paramref name="relay"/> holds an
    /// allocation, each candidate paired with our relayed candidate too: the relay binds a channel to it now, and its
    /// checks start only <see cref="IceTiming.RelayHeadStart"/> after the direct ones, so a direct path wins wherever one
    /// exists.
    /// </summary>
    public void Start(IceCredentials remote, IReadOnlyList<IceCandidate> candidates, TurnClient? relay)
    {
        _remoteIntegrity = StunIntegrity.ForPassword(remote.Password);
        _outgoingUsername = Encoding.UTF8.GetBytes($"{remote.Ufrag}:{Local.Ufrag}");
        _incomingUsername = Encoding.UTF8.GetBytes($"{Local.Ufrag}:{remote.Ufrag}");

        if (relay?.State == TurnState.Allocated)
        {
            _relay = relay;
        }

        // Room for the relayed and spare pairs too, so learning one adds without growing the list.
        _pairs.Capacity = (candidates.Count * PathsPerCandidate) + _spares.Length;

        foreach (var candidate in candidates)
        {
            if (candidate.Component != IcePriorities.RtpComponent)
            {
                continue;
            }

            _pairs.Add(new IcePair(candidate, IcePriorities.Pair(_hostPriority, candidate.Priority), false));

            if (_relay is not null && _relay.Bind(candidate.Endpoint))
            {
                _pairs.Add(new IcePair(candidate, IcePriorities.Pair(RelayPriority, candidate.Priority), true));
            }
        }

        _pairs.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
        long now = _clock.GetTimestamp();
        _nextCheckAt = now;
        _relayFrom = now + Ticks(IceTiming.RelayHeadStart);
        State = IceAgentState.Checking;

        if (_pairs.Count == 0)
        {
            State = IceAgentState.Failed;
        }
    }

    /// <summary>Sends the checks that are due: the next new one, retransmissions, and keep-alives once connected.</summary>
    [HotPath]
    public void Service()
    {
        long now = _clock.GetTimestamp();

        if (State == IceAgentState.Connected)
        {
            KeepAlive(now);
        }
        else if (State == IceAgentState.Checking)
        {
            Retransmit(now);
            StartNextCheck(now);
            FailWhenAllFailed();
        }
    }

    /// <summary>
    /// Handles a received datagram when it is STUN (answers, checks); returns false for anything else (DTLS, RTP),
    /// which the caller passes on.
    /// </summary>
    [HotPath]
    public bool TryHandle(DatagramEndpoint source, ReadOnlySpan<byte> datagram)
    {
        return HandleFrom(source, datagram, false);
    }

    /// <summary>As <see cref="TryHandle(DatagramEndpoint, ReadOnlySpan{byte})"/> for a datagram the relay passed on from <paramref name="peer"/>.</summary>
    [HotPath]
    public bool TryHandleRelayed(DatagramEndpoint peer, ReadOnlySpan<byte> datagram)
    {
        return HandleFrom(peer, datagram, true);
    }

    /// <summary>Sends <paramref name="datagram"/> (DTLS, RTCP) on the selected path, through the relay when it is relayed.</summary>
    [HotPath]
    public bool SendSelected(ReadOnlySpan<byte> datagram)
    {
        if (_selected is null)
        {
            return false;
        }

        return SendOn(_selected.Endpoint, _selected.Relayed, datagram);
    }

    private bool HandleFrom(DatagramEndpoint source, ReadOnlySpan<byte> datagram, bool relayed)
    {
        if (!StunReader.IsMessage(datagram))
        {
            return false;
        }

        if (!StunReader.HasValidFingerprint(datagram))
        {
            return true;
        }

        switch (StunReader.Type(datagram))
        {
            case StunFormat.BindingRequest:
                Answer(source, datagram, relayed);

                return true;
            case StunFormat.BindingSuccess:
                Succeed(source, datagram, relayed);

                return true;
            case StunFormat.BindingError:
                Refuse(datagram);

                return true;
            default:
                return true;
        }
    }

    private void KeepAlive(long now)
    {
        if (_selected is null || now < _keepAliveAt)
        {
            return;
        }

        NewTransaction(_selected);
        _selected.SentAt = now;
        Send(_selected);
        _keepAliveAt = now + Ticks(IceTiming.KeepAlive);
    }

    private void Retransmit(long now)
    {
        foreach (var pair in _pairs)
        {
            if (pair.State != IcePairState.InProgress || now < pair.DueAt)
            {
                continue;
            }

            if (pair.Attempts >= IceTiming.MaxAttempts)
            {
                pair.State = IcePairState.Failed;
                continue;
            }

            pair.Timeout += pair.Timeout;
            Transmit(pair, now);
        }
    }

    private void StartNextCheck(long now)
    {
        if (now < _nextCheckAt)
        {
            return;
        }

        foreach (var pair in _pairs)
        {
            if (pair.State == IcePairState.Waiting && pair.Relayed && !RelayReady(pair, now))
            {
                continue;
            }

            if (pair.State == IcePairState.Waiting)
            {
                NewTransaction(pair);
                pair.State = IcePairState.InProgress;
                pair.Timeout = IceTiming.FirstTimeout;
                Transmit(pair, now);
                _nextCheckAt = now + Ticks(IceTiming.Pacing);

                return;
            }
        }
    }

    /// <summary>
    /// A relayed pair is checked once the direct pairs had their head start and its channel is bound; a pair whose
    /// peer the relay refused (a private address) fails at once.
    /// </summary>
    private bool RelayReady(IcePair pair, long now)
    {
        if (_relay is null || _relay.IsRefused(pair.Endpoint))
        {
            pair.State = IcePairState.Failed;

            return false;
        }

        return now >= _relayFrom && _relay.IsBound(pair.Endpoint);
    }

    private void FailWhenAllFailed()
    {
        foreach (var pair in _pairs)
        {
            if (pair.State != IcePairState.Failed)
            {
                return;
            }
        }

        State = IceAgentState.Failed;
    }

    private void Transmit(IcePair pair, long now)
    {
        pair.Attempts++;
        pair.DueAt = now + Ticks(pair.Timeout);
        pair.SentAt = now;
        Send(pair);
    }

    /// <summary>A nominating check: USERNAME, PRIORITY, ICE-CONTROLLING, USE-CANDIDATE, integrity with the remote password.</summary>
    private void Send(IcePair pair)
    {
        var writer = new StunWriter(_send, StunFormat.BindingRequest, pair.TransactionId);
        writer.Add(StunFormat.Username, _outgoingUsername);
        writer.AddUInt32(StunFormat.Priority, _checkPriority);
        writer.AddUInt64(StunFormat.IceControlling, _tieBreaker);
        writer.AddFlag(StunFormat.UseCandidate);
        writer.AddIntegrity(_remoteIntegrity!);

        writer.AddFingerprint();
        SendOn(pair.Endpoint, pair.Relayed, writer.Written);
    }

    private bool SendOn(DatagramEndpoint endpoint, bool relayed, ReadOnlySpan<byte> datagram)
    {
        if (relayed)
        {
            return _relay?.TrySend(endpoint, datagram) == true;
        }

        return _transport.TrySend(endpoint, datagram);
    }

    /// <summary>
    /// Answers a remote check that carries our username and integrity, on the path it came by, and triggers our check of
    /// that pair.
    /// </summary>
    private void Answer(DatagramEndpoint source, ReadOnlySpan<byte> request, bool relayed)
    {
        if (!StunReader.TryFind(request, StunFormat.Username, out var username, out _)
            || !username.SequenceEqual(_incomingUsername)
            || !StunReader.HasValidIntegrity(request, _localIntegrity))
        {
            return;
        }

        var writer = new StunWriter(_send, StunFormat.BindingSuccess, StunReader.TransactionId(request));
        writer.AddXorMappedAddress(source);
        writer.AddIntegrity(_localIntegrity);
        writer.AddFingerprint();
        SendOn(source, relayed, writer.Written);

        TriggerCheck(source, request, relayed);
    }

    /// <summary>
    /// A check from the remote side means its path is open now: its pair is checked at once, even one that failed
    /// before (its NAT dropped our checks until its own went out), and a source no candidate named becomes a
    /// peer-reflexive pair (RFC 8445 section 7.3.1.3), as long as a spare one is left.
    /// </summary>
    private void TriggerCheck(DatagramEndpoint source, ReadOnlySpan<byte> request, bool relayed)
    {
        if (State != IceAgentState.Checking)
        {
            return;
        }

        var pair = PairAt(source, relayed) ?? Learn(source, request, relayed);

        if (pair is null || pair.State == IcePairState.InProgress || pair.State == IcePairState.Succeeded)
        {
            return;
        }

        NewTransaction(pair);
        pair.State = IcePairState.InProgress;
        pair.Timeout = IceTiming.FirstTimeout;
        Transmit(pair, _clock.GetTimestamp());
    }

    private IcePair? PairAt(DatagramEndpoint endpoint, bool relayed)
    {
        foreach (var pair in _pairs)
        {
            if (pair.Endpoint == endpoint && pair.Relayed == relayed)
            {
                return pair;
            }
        }

        return null;
    }

    /// <summary>
    /// A spare pair toward <paramref name="source"/> on the path its check came by, at the priority it announced; null
    /// when none is left.
    /// </summary>
    private IcePair? Learn(DatagramEndpoint source, ReadOnlySpan<byte> request, bool relayed)
    {
        if (_learned == _spares.Length)
        {
            return null;
        }

        uint remotePriority = _checkPriority;

        if (StunReader.TryFind(request, StunFormat.Priority, out var value, out _) && value.Length >= sizeof(uint))
        {
            remotePriority = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(value);
        }

        uint localPriority = _hostPriority;

        if (relayed)
        {
            localPriority = RelayPriority;
        }

        var pair = _spares[_learned++];
        pair.Learn(source, IcePriorities.Pair(localPriority, remotePriority), relayed);
        _pairs.Add(pair);

        return pair;
    }

    /// <summary>
    /// A success from the pair's own address, on the pair's own path, for its transaction, signed with the remote
    /// password, selects it.
    /// </summary>
    private void Succeed(DatagramEndpoint source, ReadOnlySpan<byte> response, bool relayed)
    {
        var pair = PairOf(StunReader.TransactionId(response));

        if (pair is null || pair.Endpoint != source || pair.Relayed != relayed || !StunReader.HasValidIntegrity(response, _remoteIntegrity!))
        {
            return;
        }

        pair.State = IcePairState.Succeeded;
        pair.RoundTrip = _clock.GetElapsedTime(pair.SentAt);

        if (State == IceAgentState.Checking)
        {
            _selected = pair;
            State = IceAgentState.Connected;
            _keepAliveAt = _clock.GetTimestamp() + Ticks(IceTiming.KeepAlive);
        }
    }

    private void Refuse(ReadOnlySpan<byte> response)
    {
        var pair = PairOf(StunReader.TransactionId(response));

        if (pair is not null && pair.State == IcePairState.InProgress)
        {
            pair.State = IcePairState.Failed;
        }
    }

    private IcePair? PairOf(ReadOnlySpan<byte> transactionId)
    {
        foreach (var pair in _pairs)
        {
            if (transactionId.SequenceEqual(pair.TransactionId))
            {
                return pair;
            }
        }

        return null;
    }

    private void NewTransaction(IcePair pair)
    {
        var id = pair.TransactionId.AsSpan();

        for (int offset = 0; offset < id.Length; offset += sizeof(uint))
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(id[offset..], _random.NextUInt32());
        }

        pair.Attempts = 0;
    }

    private long Ticks(TimeSpan span)
    {
        return span.Ticks * _clock.TimestampFrequency / TimeSpan.TicksPerSecond;
    }
}
