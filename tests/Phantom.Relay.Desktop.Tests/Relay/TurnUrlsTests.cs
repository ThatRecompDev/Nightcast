// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Relay.Desktop.Relay;

namespace Phantom.Relay.Desktop.Tests.Relay;

/// <summary>Cloudflare's answer, by hand in the shape its TURN docs and the devices gateway show (no real credential).</summary>
public sealed class TurnUrlsTests
{
    private const string Reply = """
        {"iceServers": [
          {"urls": ["stun:stun.example:3478", "turns:relay.example:443?transport=tcp", "turn:relay.example:3478?transport=tcp", "turn:relay.example:3478?transport=udp", "turn:relay.example:53?transport=udp"],
           "username": "user-1", "credential": "secret-1"}
        ]}
        """;

    [Fact]
    public void The_first_udp_turn_url_is_chosen_with_its_credential()
    {
        var reply = JsonSerializer.Deserialize(Reply, TurnJsonContext.Default.IceServersReply)!;

        Assert.True(TurnUrls.TryChoose(reply.IceServers!, out string host, out ushort port, out string username, out string credential));

        Assert.Equal("relay.example", host);
        Assert.Equal(3478, port);
        Assert.Equal("user-1", username);
        Assert.Equal("secret-1", credential);
    }

    [Theory]
    [InlineData("turn:relay.example", "relay.example", 3478)]
    [InlineData("turn:relay.example:53", "relay.example", 53)]
    [InlineData("TURN:relay.example:3478?transport=UDP", "relay.example", 3478)]
    public void Udp_turn_urls_are_read(string url, string host, int port)
    {
        Assert.True(TurnUrls.TryRead(url, out string readHost, out ushort readPort));

        Assert.Equal(host, readHost);
        Assert.Equal(port, readPort);
    }

    [Theory]
    [InlineData("turns:relay.example:443")]
    [InlineData("turn:relay.example:3478?transport=tcp")]
    [InlineData("stun:relay.example:3478")]
    [InlineData("turn:relay.example:port")]
    public void Other_urls_are_passed_over(string url)
    {
        Assert.False(TurnUrls.TryRead(url, out _, out _));
    }

    [Fact]
    public void A_url_given_as_one_string_and_a_server_without_credential_are_handled()
    {
        const string single = """{"iceServers": [{"urls": "turn:a.example:3478"}, {"urls": "turn:b.example:3478", "username": "u", "credential": "c"}]}""";
        var reply = JsonSerializer.Deserialize(single, TurnJsonContext.Default.IceServersReply)!;

        Assert.True(TurnUrls.TryChoose(reply.IceServers!, out string host, out _, out _, out _));

        Assert.Equal("b.example", host);
    }

    [Fact]
    public void The_key_file_reads_the_gateways_shape()
    {
        var key = JsonSerializer.Deserialize("""{"keyId": "k", "apiToken": "t"}""", TurnJsonContext.Default.TurnKey)!;

        Assert.Equal("k", key.KeyId);
        Assert.Equal("t", key.ApiToken);
    }
}
