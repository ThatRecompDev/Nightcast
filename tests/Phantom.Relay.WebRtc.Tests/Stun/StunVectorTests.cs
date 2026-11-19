// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Stun;

namespace Phantom.Relay.WebRtc.Tests.Stun;

/// <summary>The test vectors of RFC 5769, sections 2.1 (a request) and 2.2 (an IPv4 response), as data.</summary>
public sealed class StunVectorTests
{
    private const string Password = "VOkJxbRl1RmTxUk/WvJxBt";

    private static readonly byte[] TransactionId = Convert.FromHexString("b7e7a701bc34d686fa87dfae");

    private static readonly byte[] Request = Convert.FromHexString(
        "000100582112a442b7e7a701bc34d686fa87dfae"
        + "80220010" + "5354554e2074657374" + "20636c69656e74"
        + "00240004" + "6e0001ff"
        + "80290008" + "932ff9b151263b36"
        + "00060009" + "6576746a3a68367659202020"
        + "00080014" + "9aeaa70cbfd8cb56781ef2b5b2d3f249c1b571a2"
        + "80280004" + "e57a3bcf");

    private static readonly byte[] Response = Convert.FromHexString(
        "0101003c2112a442b7e7a701bc34d686fa87dfae"
        + "8022000b" + "7465737420766563746f7220"
        + "00200008" + "0001a147e112a643"
        + "00080014" + "2b91f599fd9e90c38c7489f92af9ba53f06be7d7"
        + "80280004" + "c07d4c96");

    [Fact]
    public void The_writer_reproduces_the_sample_request_byte_for_byte()
    {
        var buffer = new byte[StunFormat.MaxMessageLength];
        var writer = new StunWriter(buffer, StunFormat.BindingRequest, TransactionId);

        writer.Add(StunFormat.Software, "STUN test client"u8);
        writer.AddUInt32(StunFormat.Priority, 0x6e0001ff);
        writer.AddUInt64(StunFormat.IceControlled, 0x932ff9b151263b36);
        writer.Add(StunFormat.Username, "evtj:h6vY"u8, (byte)' ');
        writer.AddIntegrity(StunIntegrity.ForPassword(Password));
        writer.AddFingerprint();

        Assert.Equal(Convert.ToHexString(Request), Convert.ToHexString(writer.Written));
    }

    [Fact]
    public void The_sample_request_reads_with_its_attributes_integrity_and_fingerprint()
    {
        Assert.True(StunReader.IsMessage(Request));
        Assert.Equal(StunFormat.BindingRequest, StunReader.Type(Request));
        Assert.Equal(TransactionId, StunReader.TransactionId(Request).ToArray());

        Assert.True(StunReader.TryFind(Request, StunFormat.Username, out var username, out _));
        Assert.Equal("evtj:h6vY"u8.ToArray(), username.ToArray());
        Assert.True(StunReader.HasValidIntegrity(Request, StunIntegrity.ForPassword(Password)));
        Assert.True(StunReader.HasValidFingerprint(Request));
    }

    [Fact]
    public void The_sample_response_gives_its_mapped_address()
    {
        Assert.True(StunReader.IsMessage(Response));
        Assert.True(StunReader.TryFind(Response, StunFormat.XorMappedAddress, out var value, out _));

        Assert.True(StunReader.TryReadXorMappedAddress(value, out var mapped));

        Assert.Equal(DatagramEndpoint.FromOctets(192, 0, 2, 1, 32853), mapped);
        Assert.True(StunReader.HasValidIntegrity(Response, StunIntegrity.ForPassword(Password)));
        Assert.True(StunReader.HasValidFingerprint(Response));
    }

    [Fact]
    public void The_writer_reproduces_the_sample_response()
    {
        var buffer = new byte[StunFormat.MaxMessageLength];
        var writer = new StunWriter(buffer, StunFormat.BindingSuccess, TransactionId);

        writer.Add(StunFormat.Software, "test vector"u8, (byte)' ');
        writer.AddXorMappedAddress(DatagramEndpoint.FromOctets(192, 0, 2, 1, 32853));
        writer.AddIntegrity(StunIntegrity.ForPassword(Password));
        writer.AddFingerprint();

        Assert.Equal(Convert.ToHexString(Response), Convert.ToHexString(writer.Written));
    }

    [Fact]
    public void A_wrong_password_or_a_changed_byte_is_caught()
    {
        var tampered = (byte[])Request.Clone();
        tampered[24] ^= 1;

        Assert.False(StunReader.HasValidIntegrity(Request, StunIntegrity.ForPassword("wrong")));
        Assert.False(StunReader.HasValidIntegrity(tampered, StunIntegrity.ForPassword(Password)));
        Assert.False(StunReader.HasValidFingerprint(tampered));
    }

    [Fact]
    public void Dtls_and_rtp_datagrams_are_not_stun()
    {
        byte[] dtlsHandshake = [22, 254, 253, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] rtp = [0x80, 102, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.False(StunReader.IsMessage(dtlsHandshake));
        Assert.False(StunReader.IsMessage(rtp));
        Assert.False(StunReader.IsMessage(Request.AsSpan(0, 30)));
    }
}
