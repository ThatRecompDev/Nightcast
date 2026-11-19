// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.WebRtc.Tests.Ice;

public sealed class SessionIceTests
{
    private const string Answer =
        "v=0\r\no=- 1 2 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\na=group:BUNDLE 0 1 2\r\n"
        + "m=audio 9 UDP/TLS/RTP/SAVPF 111\r\na=ice-ufrag:AbCd\r\na=ice-pwd:the-console-password-123456\r\n"
        + "a=candidate:1 1 UDP 100 192.168.1.20 9002 typ host\r\na=candidate:2 1 TCP 90 192.168.1.20 9 typ host\r\n"
        + "m=video 9 UDP/TLS/RTP/SAVPF 104\r\na=ice-ufrag:AbCd\r\na=ice-pwd:the-console-password-123456\r\n";

    [Fact]
    public void The_first_credentials_of_the_description_are_read()
    {
        var credentials = SessionIce.ReadCredentials(Answer);

        Assert.Equal(new IceCredentials("AbCd", "the-console-password-123456"), credentials);
    }

    [Fact]
    public void Udp_candidates_are_read_and_tcp_ones_skipped()
    {
        var candidates = SessionIce.ReadCandidates(Answer);

        var candidate = Assert.Single(candidates);
        Assert.Equal(DatagramEndpoint.FromOctets(192, 168, 1, 20, 9002), candidate.Endpoint);
    }

    [Fact]
    public void A_description_without_credentials_gives_none()
    {
        Assert.Null(SessionIce.ReadCredentials("v=0\r\nm=audio 9 UDP/TLS/RTP/SAVPF 111\r\n"));
    }
}
