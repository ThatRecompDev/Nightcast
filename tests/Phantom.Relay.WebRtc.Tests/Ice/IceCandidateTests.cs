// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.WebRtc.Tests.Ice;

public sealed class IceCandidateTests
{
    [Fact]
    public void A_udp_ipv4_line_is_read_with_or_without_its_attribute_prefix()
    {
        Assert.True(IceCandidate.TryParse("a=candidate:1 1 UDP 100 10.0.0.2 9002 typ host ", out var candidate));

        Assert.Equal("1", candidate!.Foundation);
        Assert.Equal(1, candidate.Component);
        Assert.Equal(100u, candidate.Priority);
        Assert.Equal(DatagramEndpoint.FromOctets(10, 0, 0, 2, 9002), candidate.Endpoint);
        Assert.Equal(IceCandidateKind.Host, candidate.Kind);
    }

    [Fact]
    public void Server_reflexive_lines_keep_their_kind_and_tcp_or_ipv6_lines_are_skipped()
    {
        Assert.True(IceCandidate.TryParse("candidate:2 1 udp 1694498815 203.0.113.7 61000 typ srflx raddr 10.0.0.2 rport 9002", out var reflexive));
        Assert.Equal(IceCandidateKind.ServerReflexive, reflexive!.Kind);

        Assert.False(IceCandidate.TryParse("candidate:3 1 tcp 1518280447 10.0.0.2 9 typ host tcptype active", out _));
        Assert.False(IceCandidate.TryParse("candidate:4 1 udp 2122194687 2001:db8::1 9002 typ host", out _));
        Assert.False(IceCandidate.TryParse("end-of-candidates", out _));
    }

    [Fact]
    public void A_candidate_writes_the_line_it_was_read_from()
    {
        const string line = "candidate:1 1 udp 2130706431 192.168.1.5 50000 typ host";

        Assert.True(IceCandidate.TryParse(line, out var candidate));

        Assert.Equal(line, candidate!.ToLine());
    }

    [Fact]
    public void Priorities_follow_the_rfc_formulas()
    {
        Assert.Equal(2130706431u, IcePriorities.Candidate(IcePriorities.HostPreference, IcePriorities.SingleLocalPreference, 1));
        Assert.Equal((5UL << 32) + (2 * 9UL) + 1, IcePriorities.Pair(9, 5));
        Assert.Equal((5UL << 32) + (2 * 9UL), IcePriorities.Pair(5, 9));
    }

    [Fact]
    public void Credentials_are_long_enough_and_replay_from_the_seed()
    {
        var first = IceCredentials.Create(new SeededRandomSource(5));
        var again = IceCredentials.Create(new SeededRandomSource(5));

        Assert.Equal(first, again);
        Assert.True(first.Ufrag.Length >= 4);
        Assert.True(first.Password.Length >= 22);
    }
}
