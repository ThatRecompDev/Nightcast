// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Core.Collections;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.WebRtc.Stun;

namespace Phantom.Relay.WebRtc.Tests.Ice;

/// <summary>
/// A controlled, lite ICE peer standing in for the console: it answers checks that carry its username and password,
/// can send a check of its own, and records the answers it gets.
/// </summary>
public sealed class FakeIcePeer
{
    private readonly IDatagramTransport _transport;
    private readonly IceCredentials _own;
    private readonly IceCredentials _agent;
    private readonly ByteRing _ring = new(ByteRing.CapacityFor(StunFormat.MaxMessageLength));
    private readonly byte[] _send = new byte[StunFormat.MaxMessageLength];

    public FakeIcePeer(IDatagramTransport transport, IceCredentials own, IceCredentials agent)
    {
        _transport = transport;
        _own = own;
        _agent = agent;
    }

    public int ChecksAnswered { get; private set; }

    public bool SawUseCandidate { get; private set; }

    public List<byte[]> Answers { get; } = [];

    public IceCandidate Candidate(uint priority)
    {
        return new IceCandidate("9", 1, priority, _transport.LocalEndpoint, IceCandidateKind.Host);
    }

    /// <summary>Sends a check to the agent, as the console checks its side.</summary>
    public void Check(DatagramEndpoint agent)
    {
        var writer = new StunWriter(_send, StunFormat.BindingRequest, Convert.FromHexString("0102030405060708090a0b0c"));
        writer.Add(StunFormat.Username, Encoding.UTF8.GetBytes($"{_agent.Ufrag}:{_own.Ufrag}"));
        writer.AddUInt64(StunFormat.IceControlled, 7);
        writer.AddIntegrity(StunIntegrity.ForPassword(_agent.Password));
        writer.AddFingerprint();

        _transport.TrySend(agent, writer.Written);
    }

    public void Drain()
    {
        _transport.Receive(_ring);

        while (_ring.TryPeek(out var frame))
        {
            Handle(DatagramFrame.Source(frame), DatagramFrame.Payload(frame));
            _ring.Release();
        }
    }

    private void Handle(DatagramEndpoint source, ReadOnlySpan<byte> message)
    {
        if (StunReader.Type(message) != StunFormat.BindingRequest)
        {
            Answers.Add(message.ToArray());

            return;
        }

        bool mine = StunReader.TryFind(message, StunFormat.Username, out var username, out _)
            && username.SequenceEqual(Encoding.UTF8.GetBytes($"{_own.Ufrag}:{_agent.Ufrag}"))
            && StunReader.HasValidIntegrity(message, StunIntegrity.ForPassword(_own.Password));

        if (!mine)
        {
            return;
        }

        SawUseCandidate |= StunReader.TryFind(message, StunFormat.UseCandidate, out _, out _);
        ChecksAnswered++;

        var writer = new StunWriter(_send, StunFormat.BindingSuccess, StunReader.TransactionId(message));
        writer.AddXorMappedAddress(source);
        writer.AddIntegrity(StunIntegrity.ForPassword(_own.Password));
        writer.AddFingerprint();
        _transport.TrySend(source, writer.Written);
    }
}
