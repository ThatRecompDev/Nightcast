// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Collections;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.InMemory;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.WebRtc.Tests.Ice;
using Phantom.Relay.WebRtc.Turn;

namespace Phantom.Relay.WebRtc.Tests.Turn;

public sealed class TurnClientTests
{
    private const string Username = "1700000000:relay-user";
    private const string Password = "short-lived-credential";

    // Documentation addresses only: the client, the TURN server, its relayed address and the console.
    private static readonly DatagramEndpoint ClientAt = DatagramEndpoint.FromOctets(192, 168, 1, 5, 50000);
    private static readonly DatagramEndpoint ServerAt = DatagramEndpoint.FromOctets(203, 0, 113, 10, 3478);
    private static readonly DatagramEndpoint RelayAt = DatagramEndpoint.FromOctets(203, 0, 113, 10, 40000);
    private static readonly DatagramEndpoint ConsoleAt = DatagramEndpoint.FromOctets(198, 51, 100, 7, 9002);
    private static readonly IceCredentials ConsoleCredentials = new("cons", "console-password-0123456789");

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly ByteRing _ring = new(ByteRing.CapacityFor(DatagramFrame.FrameLength(1500)));
    private readonly InMemoryDatagramNetwork _network;
    private readonly FakeTurnServer _server;

    public TurnClientTests()
    {
        _network = new InMemoryDatagramNetwork(InMemoryLinkOptions.Perfect, _clock, new SeededRandomSource(3));
        _server = new FakeTurnServer(_network, ServerAt, RelayAt, Username, Password);
    }

    [Fact]
    public void The_allocation_answers_the_challenge_and_gives_the_relayed_and_mapped_addresses()
    {
        var (client, transport) = Client(Password, null);

        client.Start();
        Run(client, transport, 10);

        Assert.Equal(TurnState.Allocated, client.State);
        Assert.Equal(RelayAt, client.Relayed);
        Assert.Equal(ClientAt, client.Mapped);
        Assert.Equal(1, _server.Challenges);
    }

    [Fact]
    public void A_refused_credential_ends_the_allocation_with_its_error()
    {
        var (client, transport) = Client("not-the-password", null);

        client.Start();
        Run(client, transport, 10);

        Assert.Equal(TurnState.Failed, client.State);
        Assert.Equal(401, client.Refusal);
    }

    [Fact]
    public void Data_reaches_a_bound_peer_and_its_answer_comes_back_from_it()
    {
        var (client, transport) = Client(Password, null);
        var peer = _network.CreateTransport(ConsoleAt);
        var peerRing = new ByteRing(ByteRing.CapacityFor(DatagramFrame.FrameLength(1500)));
        client.Start();
        Run(client, transport, 10);

        Assert.True(client.Bind(ConsoleAt));
        Run(client, transport, 10);
        Assert.True(client.IsBound(ConsoleAt));

        Assert.True(client.TrySend(ConsoleAt, [1, 2, 3]));
        _server.Drain();
        _clock.AdvanceMilliseconds(10);

        // The console sees the relayed address and answers it.
        peer.Receive(peerRing);
        Assert.True(peerRing.TryPeek(out var arrived));
        Assert.Equal(RelayAt, DatagramFrame.Source(arrived));
        Assert.Equal([1, 2, 3], DatagramFrame.Payload(arrived).ToArray());
        peerRing.Release();

        peer.TrySend(RelayAt, [9, 8]);
        _server.Drain();
        _clock.AdvanceMilliseconds(10);

        var (from, answer) = Assert.Single(Run(client, transport, 1));
        Assert.Equal(ConsoleAt, from);
        Assert.Equal([9, 8], answer);
    }

    [Fact]
    public void A_peer_the_server_forbids_is_refused_and_nothing_is_sent_to_it()
    {
        var (client, transport) = Client(Password, null);
        _server.Forbidden.Add(ConsoleAt.Address);
        client.Start();
        Run(client, transport, 10);

        client.Bind(ConsoleAt);
        Run(client, transport, 10);

        Assert.True(client.IsRefused(ConsoleAt));
        Assert.False(client.TrySend(ConsoleAt, [1]));
    }

    [Fact]
    public void The_allocation_is_refreshed_before_its_lifetime_ends()
    {
        var (client, transport) = Client(Password, null);
        client.Start();
        Run(client, transport, 10);

        _clock.Advance(TimeSpan.FromSeconds(FakeTurnServer.LifetimeSeconds / 2));
        Run(client, transport, 10);

        Assert.Equal(1, _server.Refreshes);
        Assert.Equal(TurnState.Allocated, client.State);
    }

    [Fact]
    public void Ice_connects_through_the_relay_when_the_direct_path_is_lost()
    {
        var (relay, transport) = Client(Password, ConsoleAt);
        var agent = new IceAgent(transport, _clock, new SeededRandomSource(11), IceCredentials.Create(new SeededRandomSource(12)));
        var console = new FakeIcePeer(_network.CreateTransport(ConsoleAt), ConsoleCredentials, agent.Local);
        relay.Start();
        Run(relay, transport, 10);

        agent.Start(ConsoleCredentials, [console.Candidate(1_000_000)], relay);
        RunIce(agent, relay, transport, console, 400);

        Assert.Equal(IceAgentState.Connected, agent.State);
        Assert.True(agent.SelectedRelayed);
        Assert.Equal(ConsoleAt, agent.Selected);
        Assert.True(agent.SendSelected([23, 254, 253]));
    }

    [Fact]
    public void An_open_direct_path_wins_over_the_relay()
    {
        var (relay, transport) = Client(Password, null);
        var agent = new IceAgent(transport, _clock, new SeededRandomSource(11), IceCredentials.Create(new SeededRandomSource(12)));
        var console = new FakeIcePeer(_network.CreateTransport(ConsoleAt), ConsoleCredentials, agent.Local);
        relay.Start();
        Run(relay, transport, 10);

        agent.Start(ConsoleCredentials, [console.Candidate(1_000_000)], relay);
        RunIce(agent, relay, transport, console, 400);

        Assert.Equal(IceAgentState.Connected, agent.State);
        Assert.False(agent.SelectedRelayed);
        Assert.Equal(0, _server.ChannelDataRelayed);
    }

    [Fact]
    public void Our_candidates_name_the_relayed_and_mapped_addresses()
    {
        var (relay, transport) = Client(Password, null);
        relay.Start();
        Run(relay, transport, 10);

        Assert.Equal("candidate:3 1 udp 16777215 203.0.113.10 40000 typ relay", IceAgent.RelayCandidate(relay)!.ToLine());
        Assert.Equal(ClientAt, IceAgent.ReflexiveCandidate(relay)!.Endpoint);
    }

    private (TurnClient Client, IDatagramTransport Transport) Client(string password, DatagramEndpoint? blocked)
    {
        IDatagramTransport transport = _network.CreateTransport(ClientAt);

        if (blocked is not null)
        {
            transport = new BlockingTransport(transport, blocked.Value);
        }

        return (new TurnClient(transport, _clock, new SeededRandomSource(5), new TurnServer(ServerAt, Username, password)), transport);
    }

    /// <summary>Runs the client and the server in 10 ms steps; returns the peers' data the relay passed on.</summary>
    private List<(DatagramEndpoint Peer, byte[] Data)> Run(TurnClient client, IDatagramTransport transport, int steps)
    {
        var relayed = new List<(DatagramEndpoint, byte[])>();

        for (int step = 0; step < steps; step++)
        {
            client.Service();
            transport.Receive(_ring);

            while (_ring.TryPeek(out var frame))
            {
                if (client.TryHandle(DatagramFrame.Source(frame), DatagramFrame.Payload(frame), out var peer, out var data) && !data.IsEmpty)
                {
                    relayed.Add((peer, data.ToArray()));
                }

                _ring.Release();
            }

            _server.Drain();
            _clock.AdvanceMilliseconds(10);
        }

        return relayed;
    }

    /// <summary>Runs ICE as the stream's link does: the relay's datagrams to it first, the rest to the agent.</summary>
    private void RunIce(IceAgent agent, TurnClient relay, IDatagramTransport transport, FakeIcePeer console, int steps)
    {
        for (int step = 0; step < steps; step++)
        {
            relay.Service();
            agent.Service();
            transport.Receive(_ring);

            while (_ring.TryPeek(out var frame))
            {
                var source = DatagramFrame.Source(frame);
                var payload = DatagramFrame.Payload(frame);

                if (relay.TryHandle(source, payload, out var peer, out var data))
                {
                    if (!data.IsEmpty)
                    {
                        agent.TryHandleRelayed(peer, data);
                    }
                }
                else
                {
                    agent.TryHandle(source, payload);
                }

                _ring.Release();
            }

            _server.Drain();
            console.Drain();
            _clock.AdvanceMilliseconds(10);
        }
    }
}
