// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

public sealed class SctpHandshakeTests
{
    [Fact]
    public void The_client_connects_and_both_sides_are_established_with_partial_reliability()
    {
        var pair = new SctpPair(1);

        pair.Connect();

        Assert.Equal(SctpAssociationState.Established, pair.Client.State);
        Assert.Equal(SctpAssociationState.Established, pair.Server.State);
        Assert.True(pair.Client.PeerSupportsPartialReliability);
        Assert.True(pair.Server.PeerSupportsPartialReliability);
        Assert.Equal(1024, pair.Client.OutboundStreams);
    }

    [Fact]
    public void The_init_advertises_a_large_window_and_forward_tsn_from_port_5000_with_tag_zero()
    {
        var pair = new SctpPair(2);
        pair.ToServer.Record = true;

        pair.Client.Connect();

        var init = pair.ToServer.Recorded[0];
        int offset = SctpFormat.CommonHeaderLength;
        Assert.True(SctpPacket.TryReadChunk(init, ref offset, out byte type, out _, out var value));
        Assert.Equal(SctpFormat.Init, type);
        Assert.Equal(0u, SctpPacket.VerificationTag(init));
        Assert.Equal(5000, SctpPacket.SourcePort(init));

        Assert.True(BinaryPrimitives.ReadUInt32BigEndian(value[4..]) >= 1_000_000);
        Assert.True(HasParameter(value[SctpFormat.InitFixedLength..], SctpFormat.ForwardTsnSupported));
    }

    [Fact]
    public void A_lost_init_is_sent_again_and_the_association_still_comes_up()
    {
        var pair = new SctpPair(3);
        pair.ToServer.Blocked = true;
        pair.Client.Connect();
        pair.Run(10);

        pair.ToServer.Blocked = false;
        pair.Run(400);

        Assert.Equal(SctpAssociationState.Established, pair.Client.State);
        Assert.Equal(SctpAssociationState.Established, pair.Server.State);
    }

    [Fact]
    public void A_lost_cookie_ack_is_recovered_by_echoing_the_cookie_again()
    {
        var pair = new SctpPair(4);
        pair.Client.Connect();
        pair.Step(5);
        pair.ToClient.Blocked = true;
        pair.Step(5);

        pair.ToClient.Blocked = false;
        pair.Run(400);

        Assert.Equal(SctpAssociationState.Established, pair.Client.State);
        Assert.Equal(SctpAssociationState.Established, pair.Server.State);
    }

    [Fact]
    public void A_peer_that_never_answers_times_the_handshake_out()
    {
        var pair = new SctpPair(5);
        pair.ToServer.Blocked = true;

        pair.Client.Connect();
        pair.Run(1500, 100);

        Assert.Equal(SctpAssociationState.Closed, pair.Client.State);
        Assert.Equal(SctpCloseReason.TimedOut, pair.Client.CloseReason);
    }

    [Fact]
    public void Abort_closes_both_sides()
    {
        var pair = new SctpPair(6);
        pair.Connect();

        pair.Client.Abort();
        pair.Run(5);

        Assert.Equal(SctpCloseReason.Aborted, pair.Client.CloseReason);
        Assert.Equal(SctpAssociationState.Closed, pair.Server.State);
        Assert.Equal(SctpCloseReason.AbortedByPeer, pair.Server.CloseReason);
    }

    [Fact]
    public void Shutdown_delivers_what_was_queued_and_then_closes_both_sides()
    {
        var pair = new SctpPair(7);
        pair.Connect();
        pair.ToServer.DropRate = 0.3;
        pair.Client.Send(0, 53, [1, 2, 3], SctpSendPolicy.Reliable);
        pair.Client.Send(0, 53, [4, 5, 6], SctpSendPolicy.Reliable);

        pair.Client.Shutdown();
        bool refused = !pair.Client.Send(0, 53, [7], SctpSendPolicy.Reliable);
        pair.Run(2000);

        Assert.True(refused);
        Assert.Equal(2, SctpPair.Drain(pair.Server).Count);
        Assert.Equal(SctpCloseReason.Shutdown, pair.Client.CloseReason);
        Assert.Equal(SctpCloseReason.Shutdown, pair.Server.CloseReason);
    }

    [Fact]
    public void A_heartbeat_is_answered_with_its_information_unchanged()
    {
        var pair = new SctpPair(8);
        pair.ToServer.Record = true;
        pair.Connect();
        uint serverTag = SctpPacket.VerificationTag(pair.ToServer.Recorded[^1]);
        pair.ToClient.Record = true;

        pair.Server.Receive(Heartbeat(serverTag, [9, 8, 7, 6, 5, 4, 3, 2]));

        var answer = pair.ToClient.Recorded[^1];
        int offset = SctpFormat.CommonHeaderLength;
        Assert.True(SctpPacket.TryReadChunk(answer, ref offset, out byte type, out _, out var value));
        Assert.Equal(SctpFormat.HeartbeatAck, type);
        Assert.Equal(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 }, value[SctpFormat.ParameterHeaderLength..].ToArray());
    }

    [Fact]
    public void A_packet_with_a_bad_checksum_or_the_wrong_tag_is_ignored()
    {
        var pair = new SctpPair(9);
        pair.ToServer.Record = true;
        pair.Connect();
        uint serverTag = SctpPacket.VerificationTag(pair.ToServer.Recorded[^1]);
        pair.ToClient.Record = true;

        var corrupt = Heartbeat(serverTag, [1, 2, 3, 4]);
        corrupt[^1] ^= 1;

        pair.Server.Receive(corrupt);
        pair.Server.Receive(Heartbeat(serverTag + 1, [1, 2, 3, 4]));

        Assert.Empty(pair.ToClient.Recorded);
    }

    private static byte[] Heartbeat(uint tag, byte[] information)
    {
        var builder = new SctpPacketBuilder(1200);
        builder.Begin(5000, 5000, tag);
        var value = builder.AddChunk(SctpFormat.Heartbeat, 0, SctpPacketBuilder.ParameterLength(information.Length));
        SctpPacketBuilder.WriteParameter(value, SctpFormat.HeartbeatInfo, information);

        return builder.Seal().ToArray();
    }

    private static bool HasParameter(ReadOnlySpan<byte> parameters, ushort wanted)
    {
        int offset = 0;

        while (SctpPacket.TryReadParameter(parameters, ref offset, out ushort type, out _))
        {
            if (type == wanted)
            {
                return true;
            }
        }

        return false;
    }
}
