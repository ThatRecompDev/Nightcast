// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Collections;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.InMemory;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.WebRtc.Stun;

namespace Phantom.Relay.WebRtc.Tests.Ice;

public sealed class IceAgentTests
{
    private static readonly DatagramEndpoint AgentAt = DatagramEndpoint.FromOctets(192, 168, 1, 5, 50000);
    private static readonly DatagramEndpoint ConsoleAt = DatagramEndpoint.FromOctets(192, 168, 1, 20, 9002);
    private static readonly DatagramEndpoint OtherAt = DatagramEndpoint.FromOctets(192, 168, 1, 21, 9002);
    private static readonly DatagramEndpoint NowhereAt = DatagramEndpoint.FromOctets(10, 9, 9, 9, 9);
    private static readonly IceCredentials ConsoleCredentials = new("cons", "console-password-0123456789");

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly ByteRing _ring = new(ByteRing.CapacityFor(StunFormat.MaxMessageLength));

    [Fact]
    public void An_unreachable_better_candidate_is_passed_over_for_the_one_that_answers()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, transport) = Agent(network);
        var console = Peer(network, ConsoleAt, agent);
        var nowhere = new IceCandidate("8", 1, 2_000_000_000, NowhereAt, IceCandidateKind.Host);

        agent.Start(ConsoleCredentials, [nowhere, console.Candidate(1_000_000)]);
        Run(agent, transport, [console], 50);

        Assert.Equal(IceAgentState.Connected, agent.State);
        Assert.Equal(ConsoleAt, agent.Selected);
        Assert.True(console.SawUseCandidate);
    }

    [Fact]
    public void Of_two_answering_candidates_the_better_one_is_selected()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, transport) = Agent(network);
        var console = Peer(network, ConsoleAt, agent);
        var other = Peer(network, OtherAt, agent);

        agent.Start(ConsoleCredentials, [other.Candidate(1_000), console.Candidate(2_000_000_000)]);
        Run(agent, transport, [console, other], 50);

        Assert.Equal(ConsoleAt, agent.Selected);
        Assert.Equal(0, other.ChecksAnswered);
    }

    [Fact]
    public void Answers_signed_with_another_password_never_connect_and_the_checks_give_up()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, transport) = Agent(network);
        var impostor = new FakeIcePeer(network.CreateTransport(ConsoleAt), new IceCredentials("cons", "a-different-password-000000"), agent.Local);

        agent.Start(ConsoleCredentials, [impostor.Candidate(1_000_000)]);
        Run(agent, transport, [impostor], 1000);

        Assert.Equal(IceAgentState.Failed, agent.State);
        Assert.Null(agent.Selected);
    }

    [Fact]
    public void The_consoles_own_check_is_answered_with_its_address_and_our_signature()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, transport) = Agent(network);
        var console = Peer(network, ConsoleAt, agent);
        agent.Start(ConsoleCredentials, [console.Candidate(1_000_000)]);

        console.Check(AgentAt);
        Run(agent, transport, [console], 2);

        var answer = console.Answers.First(static a => StunReader.Type(a) == StunFormat.BindingSuccess);
        Assert.True(StunReader.TryFind(answer, StunFormat.XorMappedAddress, out var mapped, out _));
        Assert.True(StunReader.TryReadXorMappedAddress(mapped, out var address));
        Assert.Equal(ConsoleAt, address);
        Assert.True(StunReader.HasValidIntegrity(answer, StunIntegrity.ForPassword(agent.Local.Password)));
    }

    [Fact]
    public void A_lossy_link_still_connects_through_retransmissions()
    {
        var lossy = InMemoryLinkOptions.Perfect with { Latency = TimeSpan.FromMilliseconds(15), LossRate = 0.4 };
        var network = Network(lossy);
        var (agent, transport) = Agent(network);
        var console = Peer(network, ConsoleAt, agent);

        agent.Start(ConsoleCredentials, [console.Candidate(1_000_000)]);
        Run(agent, transport, [console], 1000);

        Assert.Equal(IceAgentState.Connected, agent.State);
    }

    [Fact]
    public void The_round_trip_of_the_selected_path_is_measured()
    {
        var slow = InMemoryLinkOptions.Perfect with { Latency = TimeSpan.FromMilliseconds(25) };
        var network = Network(slow);
        var (agent, transport) = Agent(network);
        var console = Peer(network, ConsoleAt, agent);

        agent.Start(ConsoleCredentials, [console.Candidate(1_000_000)]);
        Run(agent, transport, [console], 20);

        Assert.NotNull(agent.RoundTrip);
        Assert.InRange(agent.RoundTrip!.Value, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(80));
    }

    [Fact]
    public void Once_connected_the_selected_pair_is_kept_alive()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, transport) = Agent(network);
        var console = Peer(network, ConsoleAt, agent);
        agent.Start(ConsoleCredentials, [console.Candidate(1_000_000)]);
        Run(agent, transport, [console], 10);
        int answeredWhenConnected = console.ChecksAnswered;

        Run(agent, transport, [console], 1000);

        Assert.True(console.ChecksAnswered >= answeredWhenConnected + 4);
        Assert.Equal(IceAgentState.Connected, agent.State);
    }

    [Fact]
    public void Datagrams_that_are_not_stun_are_left_to_the_caller()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, _) = Agent(network);

        Assert.False(agent.TryHandle(ConsoleAt, [22, 254, 253, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Without_candidates_the_agent_fails_at_once()
    {
        var network = Network(InMemoryLinkOptions.Perfect);
        var (agent, _) = Agent(network);

        agent.Start(ConsoleCredentials, []);

        Assert.Equal(IceAgentState.Failed, agent.State);
    }

    private static FakeIcePeer Peer(InMemoryDatagramNetwork network, DatagramEndpoint at, IceAgent agent)
    {
        return new FakeIcePeer(network.CreateTransport(at), ConsoleCredentials, agent.Local);
    }

    private InMemoryDatagramNetwork Network(InMemoryLinkOptions options)
    {
        return new InMemoryDatagramNetwork(options, _clock, new SeededRandomSource(3));
    }

    private (IceAgent Agent, IDatagramTransport Transport) Agent(InMemoryDatagramNetwork network)
    {
        var transport = network.CreateTransport(AgentAt);
        var random = new SeededRandomSource(11);

        return (new IceAgent(transport, _clock, random, IceCredentials.Create(random)), transport);
    }

    /// <summary>Runs the agent and the peers in 10 ms steps.</summary>
    private void Run(IceAgent agent, IDatagramTransport transport, FakeIcePeer[] peers, int steps)
    {
        for (int step = 0; step < steps; step++)
        {
            agent.Service();
            transport.Receive(_ring);

            while (_ring.TryPeek(out var frame))
            {
                agent.TryHandle(DatagramFrame.Source(frame), DatagramFrame.Payload(frame));
                _ring.Release();
            }

            foreach (var peer in peers)
            {
                peer.Drain();
            }

            _clock.AdvanceMilliseconds(10);
        }
    }
}
