// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.WebRtc.Tests.Ice;

/// <summary>
/// RFC 4380's layout on a hand-made address: Teredo server 65.54.227.120, flags 0x8000, the client behind its NAT at
/// 192.0.2.45 (a documentation address) port 40000, the last two inverted: 2001:0:4136:e378:8000:63bf:3fff:fdd2.
/// </summary>
public sealed class TeredoAddressTests
{
    private const string Teredo = "2001:0:4136:e378:8000:63bf:3fff:fdd2";
    private static readonly uint ClientAddress = (192u << 24) | (2u << 8) | 45u;

    [Fact]
    public void A_teredo_address_gives_the_clients_external_ipv4_address_and_port()
    {
        Assert.True(TeredoAddress.TryRead(Teredo, out uint address, out ushort port));

        Assert.Equal(ClientAddress, address);
        Assert.Equal(40000, port);
    }

    [Theory]
    [InlineData("2001:db8::1")]
    [InlineData("fe80::1")]
    [InlineData("192.0.2.45")]
    [InlineData("not an address")]
    public void Anything_but_a_teredo_address_is_refused(string text)
    {
        Assert.False(TeredoAddress.TryRead(text, out _, out _));
    }

    [Fact]
    public void A_teredo_candidate_line_becomes_the_routers_address_at_its_own_port_and_the_tunnels()
    {
        var read = new List<IceCandidate>();

        int added = IceCandidate.ReadInto($"a=candidate:3 1 UDP 1 {Teredo} 9002 typ host", read);

        Assert.Equal(2, added);
        Assert.Equal(new DatagramEndpoint(ClientAddress, 9002), read[0].Endpoint);
        Assert.Equal(new DatagramEndpoint(ClientAddress, 40000), read[1].Endpoint);
        Assert.All(read, static c => Assert.Equal(IceCandidateKind.ServerReflexive, c.Kind));
        Assert.True(read[0].Priority > read[1].Priority);
    }

    [Fact]
    public void Ipv4_lines_are_read_as_they_are_and_other_ipv6_lines_add_nothing()
    {
        var read = new List<IceCandidate>();

        Assert.Equal(1, IceCandidate.ReadInto("candidate:1 1 udp 100 10.0.0.2 9002 typ host", read));
        Assert.Equal(0, IceCandidate.ReadInto("candidate:2 1 udp 100 2001:db8::1 9002 typ host", read));
        Assert.Equal(0, IceCandidate.ReadInto($"candidate:3 1 tcp 100 {Teredo} 9 typ host", read));

        Assert.Single(read);
    }

    [Fact]
    public void A_session_descriptions_teredo_candidate_is_read_too()
    {
        string sdp = $"v=0\r\na=ice-ufrag:abcd\r\na=ice-pwd:0123456789012345678901\r\na=candidate:1 1 UDP 100 10.0.0.2 9002 typ host\r\na=candidate:2 1 UDP 1 {Teredo} 9002 typ host\r\n";

        var candidates = SessionIce.ReadCandidates(sdp);

        Assert.Equal(3, candidates.Count);
    }
}
