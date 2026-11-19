// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Srtp;

/// <summary>A sending and a receiving context on one master key: rollover counting, the replay window, forgeries.</summary>
public sealed class SrtpContextTests
{
    private const uint Ssrc = 0x11223344;
    private const int PayloadLength = 200;

    private static readonly byte[] Key = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");

    [Theory]
    [InlineData(SrtpProfile.AesCm128HmacSha1Tag80)]
    [InlineData(SrtpProfile.AeadAes128Gcm)]
    public void Packets_across_a_sequence_wrap_unprotect_and_the_rollover_counter_counts_on(SrtpProfile profile)
    {
        var (sender, receiver) = Pair(profile);

        for (int i = 0; i < 12; i++)
        {
            ushort sequence = (ushort)(65530 + i);
            var packet = Protect(sender, sequence);

            Assert.True(receiver.TryUnprotectRtp(packet, out int length));
            Assert.Equal(Packet(sequence), packet.AsSpan(0, length).ToArray());
        }

        Assert.Equal(1u, receiver.SourceOf(Ssrc)!.RolloverCounter);
    }

    [Fact]
    public void A_late_packet_from_before_the_wrap_still_unprotects_with_the_old_rollover_counter()
    {
        var (sender, receiver) = Pair(SrtpProfile.AesCm128HmacSha1Tag80);
        var first = Protect(sender, 65534);
        var early = Protect(sender, 65535);
        var later = Protect(sender, 0);

        Assert.True(receiver.TryUnprotectRtp(first, out _));
        Assert.True(receiver.TryUnprotectRtp(later, out _));
        Assert.True(receiver.TryUnprotectRtp(early, out _));

        Assert.Equal(1u, receiver.SourceOf(Ssrc)!.RolloverCounter);
    }

    [Fact]
    public void A_replayed_packet_is_refused()
    {
        var (sender, receiver) = Pair(SrtpProfile.AeadAes128Gcm);
        var packet = Protect(sender, 100);
        var copy = (byte[])packet.Clone();

        Assert.True(receiver.TryUnprotectRtp(packet, out _));

        Assert.False(receiver.TryUnprotectRtp(copy, out _));
    }

    [Fact]
    public void A_packet_older_than_the_window_is_refused_and_one_inside_it_is_not()
    {
        var (sender, receiver) = Pair(SrtpProfile.AesCm128HmacSha1Tag80);
        var tooOld = Protect(sender, 10);
        var inside = Protect(sender, 11 + 1);
        var newest = Protect(sender, (ushort)(10 + SrtpContext.ReplayWindowPackets));

        Assert.True(receiver.TryUnprotectRtp(newest, out _));

        Assert.False(receiver.TryUnprotectRtp(tooOld, out _));
        Assert.True(receiver.TryUnprotectRtp(inside, out _));
    }

    [Fact]
    public void A_forged_packet_neither_unprotects_nor_claims_a_stream()
    {
        var (sender, receiver) = Pair(SrtpProfile.AesCm128HmacSha1Tag80);
        var packet = Protect(sender, 5);
        packet[^1] ^= 1;

        Assert.False(receiver.TryUnprotectRtp(packet, out _));

        Assert.Null(receiver.SourceOf(Ssrc));
    }

    [Fact]
    public void Ssrcs_beyond_the_streams_number_are_refused()
    {
        var (sender, receiver) = Pair(SrtpProfile.AeadAes128Gcm);

        Assert.True(receiver.TryUnprotectRtp(Protect(sender, 1, 1), out _));
        Assert.True(receiver.TryUnprotectRtp(Protect(sender, 1, 2), out _));

        Assert.False(receiver.TryUnprotectRtp(Protect(sender, 1, 3), out _));
    }

    [Theory]
    [InlineData(SrtpProfile.AesCm128HmacSha1Tag80)]
    [InlineData(SrtpProfile.AeadAes128Gcm)]
    public void Rtcp_is_protected_with_rising_indices_and_a_replay_is_refused(SrtpProfile profile)
    {
        var (sender, receiver) = Pair(profile);
        var first = ProtectRtcp(sender);
        var second = ProtectRtcp(sender);
        var replay = (byte[])second.Clone();

        Assert.True(receiver.TryUnprotectRtcp(first, out int length));
        Assert.Equal(Rtcp(), first.AsSpan(0, length).ToArray());
        Assert.True(receiver.TryUnprotectRtcp(second, out _));

        Assert.False(receiver.TryUnprotectRtcp(replay, out _));
    }

    [Fact]
    public void The_exporter_material_splits_into_client_and_server_keys()
    {
        var material = Enumerable.Range(0, 60).Select(i => (byte)i).ToArray();

        SrtpKeyingMaterial.Split(SrtpProfile.AesCm128HmacSha1Tag80, material, true, out var local, out var remote);

        Assert.Equal(60, SrtpKeyingMaterial.ExporterLength(SrtpProfile.AesCm128HmacSha1Tag80));
        Assert.Equal(56, SrtpKeyingMaterial.ExporterLength(SrtpProfile.AeadAes128Gcm));
        Assert.Equal(material[..16], local.Key);
        Assert.Equal(material[32..46], local.Salt);
        Assert.Equal(material[16..32], remote.Key);
        Assert.Equal(material[46..60], remote.Salt);
    }

    [Theory]
    [InlineData(SrtpProfile.AesCm128HmacSha1Tag80)]
    [InlineData(SrtpProfile.AeadAes128Gcm)]
    public void Unprotecting_a_stream_allocates_nothing(SrtpProfile profile)
    {
        var (sender, receiver) = Pair(profile);
        var buffer = new byte[RtpLength() + SrtpFormat.GcmTagLength];
        ushort sequence = 0;

        var report = AllocationMeter.Measure(() =>
        {
            Write(buffer, sequence++, Ssrc);
            Assert.True(sender.TryProtectRtp(buffer, RtpLength(), out int length));
            Assert.True(receiver.TryUnprotectRtp(buffer.AsSpan(0, length), out _));
        });

        Assert.Equal(0, report.BytesPerRun);
    }

    private static (SrtpContext Sender, SrtpContext Receiver) Pair(SrtpProfile profile)
    {
        var master = new SrtpMasterKey(Key, new byte[SrtpSessionKeys.SaltLength(profile)]);

        return (SrtpContext.Create(profile, master, 4), SrtpContext.Create(profile, master, 2));
    }

    private static byte[] Protect(SrtpContext sender, ushort sequence)
    {
        return Protect(sender, sequence, Ssrc);
    }

    private static byte[] Protect(SrtpContext sender, ushort sequence, uint ssrc)
    {
        var buffer = new byte[RtpLength() + sender.RtpOverhead];
        Write(buffer, sequence, ssrc);

        Assert.True(sender.TryProtectRtp(buffer, RtpLength(), out int length));

        return buffer.AsSpan(0, length).ToArray();
    }

    private static byte[] ProtectRtcp(SrtpContext sender)
    {
        var rtcp = Rtcp();
        var buffer = new byte[rtcp.Length + sender.RtcpOverhead];
        rtcp.CopyTo(buffer, 0);

        Assert.True(sender.TryProtectRtcp(buffer, rtcp.Length, out int length));

        return buffer.AsSpan(0, length).ToArray();
    }

    private static int RtpLength()
    {
        return 12 + PayloadLength;
    }

    private static byte[] Packet(ushort sequence)
    {
        var packet = new byte[RtpLength()];
        Write(packet, sequence, Ssrc);

        return packet;
    }

    private static void Write(byte[] buffer, ushort sequence, uint ssrc)
    {
        buffer[0] = 0x80;
        buffer[1] = 96;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), sequence);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4), sequence * 1500u);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8), ssrc);

        for (int i = 12; i < RtpLength(); i++)
        {
            buffer[i] = (byte)(i + sequence);
        }
    }

    private static byte[] Rtcp()
    {
        return [0x81, 0xC9, 0x00, 0x07, 0x11, 0x22, 0x33, 0x44, .. Enumerable.Range(0, 24).Select(i => (byte)(i * 3))];
    }
}
