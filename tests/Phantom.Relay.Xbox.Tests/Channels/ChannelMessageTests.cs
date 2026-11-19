// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Xbox.Channels;

namespace Phantom.Relay.Xbox.Tests.Channels;

public sealed class ChannelMessageTests
{
    [Fact]
    public void The_control_channel_opens_with_the_fixed_authorization_and_the_first_pad()
    {
        Assert.Equal("""{"message":"authorizationRequest","accessKey":"4BDB3609-C1F1-4195-9B37-FEFF45DA8B8E"}""", ControlMessages.Authorization());
        Assert.Equal("""{"message":"gamepadChanged","gamepadIndex":0,"wasAdded":true}""", ControlMessages.GamepadChanged(0, true));
        Assert.Equal("""{"message":"videoKeyframeRequested","ifrRequested":true}""", ControlMessages.KeyframeRequest());
    }

    [Fact]
    public void The_handshake_names_the_message_protocol()
    {
        Assert.Equal("""{"type":"Handshake","version":"messageV1","id":"h1","cv":"0"}""", MessageChannel.Handshake("h1"));
    }

    [Fact]
    public void The_handshake_acknowledgement_is_answered_with_the_clients_description()
    {
        var client = new ClientDescription(1920, 1080, "install", ["a", "b", "c", "d", "e", "f"]);

        var replies = MessageChannel.Answer("""{"type":"HandshakeAck","id":"x","cv":"0"}""", client);

        Assert.Equal(ClientDescription.MessageCount, replies.Count);
        Assert.Contains("\"target\":\"/streaming/systemUi/configuration\"", replies[0], StringComparison.Ordinal);
        Assert.Contains("\"id\":\"f\"", replies[5], StringComparison.Ordinal);
        Assert.Contains("\\u0022preferredWidth\\u0022:1920", replies[5], StringComparison.Ordinal);
    }

    [Fact]
    public void A_transaction_is_completed_and_anything_else_is_ignored()
    {
        var client = new ClientDescription(1280, 720, "install", ["a", "b", "c", "d", "e", "f"]);

        var completion = MessageChannel.Answer("""{"type":"TransactionStart","id":"t7","target":"/x","content":"{}"}""", client);
        var other = MessageChannel.Answer("""{"type":"Message","id":"m1","target":"/y","content":"{}"}""", client);
        var garbage = MessageChannel.Answer("not json", client);

        Assert.Equal("""{"type":"TransactionComplete","content":"","id":"t7","target":null,"cv":""}""", Assert.Single(completion));
        Assert.Empty(other);
        Assert.Empty(garbage);
    }

    [Fact]
    public void Message_ids_replay_from_the_seed()
    {
        string first = MessageIds.Next(new SeededRandomSource(42));
        string again = MessageIds.Next(new SeededRandomSource(42));

        Assert.Equal(first, again);
        Assert.True(Guid.TryParse(first, out _));
    }
}
