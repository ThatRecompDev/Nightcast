// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

public sealed class DataChannelTests
{
    /// <summary>The channels a remote-play client opens to the console, as test data.</summary>
    private static readonly DataChannelOptions[] ConsoleChannels =
    [
        DataChannelOptions.Reliable("input", "1.0"),
        DataChannelOptions.Reliable("control", "controlV1"),
        DataChannelOptions.Reliable("message", "messageV1"),
        DataChannelOptions.Reliable("chat", "chatV1"),
    ];

    [Fact]
    public void An_open_is_acknowledged_and_both_sides_see_the_channel_open()
    {
        var (pair, client, server) = Connected(31);

        var input = client.Open(ConsoleChannels[0]);
        bool openingBeforeAck = input.State == DataChannelState.Opening;
        Pump(pair, client, server);

        Assert.True(openingBeforeAck);
        Assert.Equal(DataChannelState.Open, input.State);

        Assert.True(server.TryTakeOpenedByPeer(out var accepted));
        Assert.Equal("input", accepted.Label);
        Assert.Equal("1.0", accepted.Protocol);
        Assert.True(accepted.Options.Ordered);
        Assert.Equal(DataChannelState.Open, accepted.State);
    }

    [Fact]
    public void The_dtls_client_opens_the_consoles_channels_on_even_streams_with_their_protocols()
    {
        var (pair, client, server) = Connected(32);

        var opened = ConsoleChannels.Select(client.Open).ToList();
        Pump(pair, client, server);

        Assert.Equal([0, 2, 4, 6], opened.Select(static channel => (int)channel.Id));
        Assert.All(opened, static channel => Assert.Equal(DataChannelState.Open, channel.State));
        var accepted = TakeAll(server);
        Assert.Equal(["input", "control", "message", "chat"], accepted.Select(static channel => channel.Label));
        Assert.Equal(["1.0", "controlV1", "messageV1", "chatV1"], accepted.Select(static channel => channel.Protocol));
        Assert.Equal([0, 2, 4, 6], accepted.Select(static channel => (int)channel.Id));
    }

    [Fact]
    public void The_dtls_server_opens_on_odd_streams()
    {
        var (pair, client, server) = Connected(33);

        var first = server.Open(DataChannelOptions.Reliable("a", string.Empty));
        var second = server.Open(DataChannelOptions.Reliable("b", string.Empty));
        Pump(pair, client, server);

        Assert.Equal(1, first.Id);
        Assert.Equal(3, second.Id);
        Assert.Equal(["a", "b"], TakeAll(client).Select(static channel => channel.Label));
    }

    [Fact]
    public void Strings_binaries_and_their_empty_forms_arrive_as_sent()
    {
        var (pair, client, server) = Connected(34);
        var chat = client.Open(ConsoleChannels[3]);
        Pump(pair, client, server);
        server.TryTakeOpenedByPeer(out var accepted);

        client.SendString(chat, "hello"u8);
        client.SendBinary(chat, [1, 2, 3]);
        client.SendString(chat, ReadOnlySpan<byte>.Empty);
        client.SendBinary(chat, ReadOnlySpan<byte>.Empty);
        var received = Pump(pair, client, server);

        Assert.All(received, message => Assert.Same(accepted, message.Channel));
        Assert.Equal([true, false, true, false], received.Select(static message => message.IsString));
        Assert.Equal("hello", Encoding.UTF8.GetString(received[0].Payload));
        Assert.Equal(new byte[] { 1, 2, 3 }, received[1].Payload);
        Assert.Empty(received[2].Payload);
        Assert.Empty(received[3].Payload);
    }

    [Fact]
    public void Messages_carry_the_payload_protocol_identifiers_of_rfc_8831()
    {
        var pair = new SctpPair(35);
        pair.Connect();
        var client = new DataChannelHub(pair.Client, DataChannelSide.DtlsClient);
        var channel = client.Open(ConsoleChannels[1]);

        client.SendString(channel, "x"u8);
        client.SendBinary(channel, [7]);
        client.SendString(channel, ReadOnlySpan<byte>.Empty);
        client.SendBinary(channel, ReadOnlySpan<byte>.Empty);
        pair.Run(20);

        var raw = SctpPair.Drain(pair.Server);
        Assert.Equal([50u, 51u, 53u, 56u, 57u], raw.Select(static message => message.Protocol));
        Assert.Equal(new byte[] { 0 }, raw[3].Payload);
        Assert.Equal(new byte[] { 0 }, raw[4].Payload);
    }

    [Fact]
    public void A_partially_reliable_unordered_channel_announces_its_policy_to_the_peer()
    {
        var (pair, client, server) = Connected(36);
        var options = new DataChannelOptions("telemetry", "t1", false, SctpReliability.Lifetime, 150, 512);

        client.Open(options);
        Pump(pair, client, server);

        Assert.True(server.TryTakeOpenedByPeer(out var accepted));
        Assert.Equal(options, accepted.Options);
    }

    [Fact]
    public void The_open_message_has_rfc_8832s_layout()
    {
        var options = new DataChannelOptions("chat", "chatV1", false, SctpReliability.MaxRetransmits, 3, 256);

        var message = DataChannelProtocol.Open(options);

        byte[] expected = [0x03, 0x81, 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00, 0x06, .. "chat"u8.ToArray(), .. "chatV1"u8.ToArray()];
        Assert.Equal(expected, message);
        Assert.True(DataChannelProtocol.TryReadOpen(message, out var read));
        Assert.Equal(options, read);
    }

    [Fact]
    public void A_truncated_open_is_refused()
    {
        var message = DataChannelProtocol.Open(DataChannelOptions.Reliable("input", "1.0"));

        Assert.False(DataChannelProtocol.TryReadOpen(message.AsSpan(0, message.Length - 1), out _));
    }

    private static (SctpPair Pair, DataChannelHub Client, DataChannelHub Server) Connected(int seed)
    {
        var pair = new SctpPair(seed);
        pair.Connect();

        return (pair, new DataChannelHub(pair.Client, DataChannelSide.DtlsClient), new DataChannelHub(pair.Server, DataChannelSide.DtlsServer));
    }

    /// <summary>Runs the pair, polling both hubs every step; returns what the server's channels received.</summary>
    private static List<(DataChannel Channel, bool IsString, byte[] Payload)> Pump(SctpPair pair, DataChannelHub client, DataChannelHub server)
    {
        var received = new List<(DataChannel Channel, bool IsString, byte[] Payload)>();

        for (int step = 0; step < 40; step++)
        {
            pair.Step(SctpPair.StepMilliseconds);
            Drain(client, null);
            Drain(server, received);
        }

        return received;
    }

    private static void Drain(DataChannelHub hub, List<(DataChannel Channel, bool IsString, byte[] Payload)>? into)
    {
        while (hub.TryReceive(out var channel, out bool isString, out var payload))
        {
            into?.Add((channel, isString, payload.ToArray()));
            hub.Release();
        }
    }

    private static List<DataChannel> TakeAll(DataChannelHub hub)
    {
        var channels = new List<DataChannel>();

        while (hub.TryTakeOpenedByPeer(out var channel))
        {
            channels.Add(channel);
        }

        return channels;
    }
}
