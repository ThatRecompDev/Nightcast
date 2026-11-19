// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Stun;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// A controlling ICE agent (RFC 8445) with one host candidate and one component, as a pure state machine over a
/// datagram transport: <see cref="Start"/> with the remote side's credentials and candidates, then <see cref="Service"/>
/// every frame and hand every received datagram to <see cref="TryHandle"/>. Checks go out best pair first, paced, each
/// nominating its pair (USE-CANDIDATE), so the first pair that answers is selected at once: the fastest path to a
/// connection. The remote side's checks are answered. Nothing is allocated after <see cref="Start"/>.
/// </summary>
public sealed class IceAgent
{
    private const int WordBits = 32;

    private readonly IDatagramTransport _transport;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly StunIntegrity _localIntegrity;
    private readonly ulong _tieBreaker;
    private readonly uint _checkPriority;
    private readonly byte[] _send = new byte[StunFormat.MaxMessageLength];
    private readonly List<IcePair> _pairs = [];

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
    }

    public IceCredentials Local { get; }

    public IceAgentState State { get; private set; }

    /// <summary>The remote endpoint of the selected pair, once <see cref="State"/> is <see cref="IceAgentState.Connected"/>.</summary>
    public DatagramEndpoint? Selected => _selected?.Endpoint;

    public IReadOnlyList<IcePair> Pairs => _pairs;

    /// <summary>This agent's host candidate for the signaling channel, at <paramref name="address"/> (the machine's own).</summary>
    public IceCandidate HostCandidate(uint address)
    {
        uint priority = IcePriorities.Candidate(IcePriorities.HostPreference, IcePriorities.SingleLocalPreference, IcePriorities.RtpComponent);

        return new IceCandidate("1", IcePriorities.RtpComponent, priority, new DatagramEndpoint(address, _transport.LocalEndpoint.Port), IceCandidateKind.Host);
    }

    /// <summary>Pairs every remote RTP-component candidate with the host candidate, best first, and starts checking.</summary>
    public void Start(IceCredentials remote, IReadOnlyList<IceCandidate> candidates)
    {
        _remoteIntegrity = StunIntegrity.ForPassword(remote.Password);
        _outgoingUsername = Encoding.UTF8.GetBytes($"{remote.Ufrag}:{Local.Ufrag}");
        _incomingUsername = Encoding.UTF8.GetBytes($"{Local.Ufrag}:{remote.Ufrag}");
        uint hostPriority = HostCandidate(0).Priority;

        foreach (var candidate in candidates)
        {
            if (candidate.Component == IcePriorities.RtpComponent)
            {
                _pairs.Add(new IcePair(candidate, IcePriorities.Pair(hostPriority, candidate.Priority)));
            }
        }

        _pairs.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
        _nextCheckAt = _clock.GetTimestamp();
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
                Answer(source, datagram);

                return true;
            case StunFormat.BindingSuccess:
                Succeed(source, datagram);

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
        _transport.TrySend(pair.Endpoint, writer.Written);
    }

    /// <summary>Answers a remote check that carries our username and integrity, and triggers our check of that pair.</summary>
    private void Answer(DatagramEndpoint source, ReadOnlySpan<byte> request)
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
        _transport.TrySend(source, writer.Written);

        TriggerCheck(source);
    }

    private void TriggerCheck(DatagramEndpoint source)
    {
        foreach (var pair in _pairs)
        {
            if (pair.Endpoint == source && pair.State == IcePairState.Waiting && State == IceAgentState.Checking)
            {
                NewTransaction(pair);
                pair.State = IcePairState.InProgress;
                pair.Timeout = IceTiming.FirstTimeout;
                Transmit(pair, _clock.GetTimestamp());

                return;
            }
        }
    }

    /// <summary>A success from the pair's own address for its transaction, signed with the remote password, selects it.</summary>
    private void Succeed(DatagramEndpoint source, ReadOnlySpan<byte> response)
    {
        var pair = PairOf(StunReader.TransactionId(response));

        if (pair is null || pair.Endpoint != source || !StunReader.HasValidIntegrity(response, _remoteIntegrity!))
        {
            return;
        }

        pair.State = IcePairState.Succeeded;

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
