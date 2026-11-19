// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

public sealed class SctpPacketTests
{
    [Fact]
    public void A_built_packet_carries_its_header_and_a_valid_checksum()
    {
        var builder = new SctpPacketBuilder(1200);
        builder.Begin(5000, 5000, 0x01020304);
        builder.AddChunk(SctpFormat.CookieAck, 0, 0);

        var packet = builder.Seal().ToArray();

        Assert.Equal(16, packet.Length);
        Assert.Equal(5000, SctpPacket.SourcePort(packet));
        Assert.Equal(5000, SctpPacket.DestinationPort(packet));
        Assert.Equal(0x01020304u, SctpPacket.VerificationTag(packet));
        Assert.True(SctpPacket.HasValidChecksum(packet));
    }

    [Fact]
    public void The_checksum_is_stored_little_endian_over_the_packet_with_a_zero_checksum_field()
    {
        var builder = new SctpPacketBuilder(1200);
        builder.Begin(5000, 5000, 7);
        builder.AddChunk(SctpFormat.CookieAck, 0, 0);
        var packet = builder.Seal().ToArray();

        var zeroed = packet.ToArray();
        zeroed.AsSpan(8, 4).Clear();

        Assert.Equal(SctpCrc32c.Hash(zeroed), BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)));
    }

    [Fact]
    public void A_flipped_bit_fails_the_checksum()
    {
        var builder = new SctpPacketBuilder(1200);
        builder.Begin(5000, 5000, 7);
        builder.AddChunk(SctpFormat.Heartbeat, 0, 3).Fill(9);
        var packet = builder.Seal().ToArray();

        packet[17] ^= 0x10;

        Assert.False(SctpPacket.HasValidChecksum(packet));
    }

    [Fact]
    public void Chunks_are_padded_to_four_bytes_and_read_back_in_order()
    {
        var builder = new SctpPacketBuilder(1200);
        builder.Begin(5000, 5000, 7);
        builder.AddChunk(SctpFormat.Heartbeat, 0, 5).Fill(0xAB);
        builder.AddChunk(SctpFormat.Abort, SctpFormat.TagReflectedFlag, 0);
        var packet = builder.Seal().ToArray();
        int offset = SctpFormat.CommonHeaderLength;

        Assert.True(SctpPacket.TryReadChunk(packet, ref offset, out byte first, out _, out var firstValue));
        Assert.True(SctpPacket.TryReadChunk(packet, ref offset, out byte second, out byte secondFlags, out var secondValue));
        Assert.False(SctpPacket.TryReadChunk(packet, ref offset, out _, out _, out _));

        Assert.Equal(SctpFormat.Heartbeat, first);
        Assert.Equal(new byte[] { 0xAB, 0xAB, 0xAB, 0xAB, 0xAB }, firstValue.ToArray());
        Assert.Equal(SctpFormat.Abort, second);
        Assert.Equal(SctpFormat.TagReflectedFlag, secondFlags);
        Assert.Equal(0, secondValue.Length);
        Assert.Equal(12 + 12 + 4, packet.Length);
    }

    [Fact]
    public void A_chunk_longer_than_the_packet_is_not_read()
    {
        var builder = new SctpPacketBuilder(1200);
        builder.Begin(5000, 5000, 7);
        builder.AddChunk(SctpFormat.Heartbeat, 0, 8);
        var packet = builder.Seal().ToArray();
        int offset = SctpFormat.CommonHeaderLength;

        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(14), 400);

        Assert.False(SctpPacket.TryReadChunk(packet, ref offset, out _, out _, out _));
    }

    [Fact]
    public void Parameters_are_written_padded_and_read_back()
    {
        var parameters = new byte[SctpPacketBuilder.ParameterLength(3) + SctpPacketBuilder.ParameterLength(0)];
        int written = SctpPacketBuilder.WriteParameter(parameters, SctpFormat.StateCookie, [1, 2, 3]);
        SctpPacketBuilder.WriteParameter(parameters.AsSpan(written), SctpFormat.ForwardTsnSupported, []);
        int offset = 0;

        Assert.True(SctpPacket.TryReadParameter(parameters, ref offset, out ushort cookieType, out var cookie));
        Assert.True(SctpPacket.TryReadParameter(parameters, ref offset, out ushort forwardType, out var forward));

        Assert.Equal(8, written);
        Assert.Equal(SctpFormat.StateCookie, cookieType);
        Assert.Equal(new byte[] { 1, 2, 3 }, cookie.ToArray());
        Assert.Equal(SctpFormat.ForwardTsnSupported, forwardType);
        Assert.Equal(0, forward.Length);
    }
}
