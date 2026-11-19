// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Dtls;

public sealed class DtlsHandshakeTests
{
    private const int Limit = 1200;

    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public void Both_sides_finish_with_the_same_srtp_keys_and_each_other_certificate()
    {
        var crypto = new BcTlsCrypto(new SecureRandom());
        var clientCertificate = DtlsCertificate.Create(new SecureRandom(), Now);
        var serverCertificate = DtlsCertificate.Create(new SecureRandom(), Now);
        var client = new DtlsSrtpClient(crypto, clientCertificate, serverCertificate.Fingerprint);
        var server = new FakeDtlsServer(crypto, serverCertificate);

        Handshake(client, server);

        Assert.Equal(SrtpProfile.AeadAes128Gcm, client.Profile);
        Assert.Equal(CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256, client.CipherSuite);
        Assert.Equal(server.Material, client.KeyingMaterial);
        Assert.Equal(clientCertificate.Encoded, server.ClientCertificate);
    }

    [Fact]
    public void A_server_whose_certificate_does_not_match_the_signaled_fingerprint_is_refused()
    {
        var crypto = new BcTlsCrypto(new SecureRandom());
        var clientCertificate = DtlsCertificate.Create(new SecureRandom(), Now);
        var serverCertificate = DtlsCertificate.Create(new SecureRandom(), Now);
        var someoneElse = DtlsCertificate.Create(new SecureRandom(), Now);
        var client = new DtlsSrtpClient(crypto, clientCertificate, someoneElse.Fingerprint);

        var error = Record.Exception(() => Handshake(client, new FakeDtlsServer(crypto, serverCertificate)));

        Assert.NotNull(error);
        Assert.Null(client.KeyingMaterial);
    }

    [Fact]
    public void The_fingerprint_is_read_from_a_description_and_matches_its_certificate()
    {
        var certificate = DtlsCertificate.Create(new SecureRandom(), Now);
        string sdp = $"v=0\r\nm=audio 9 UDP/TLS/RTP/SAVPF 111\r\na=fingerprint:sha-256 {certificate.Fingerprint.ToLowerInvariant()}\r\n";

        string? read = DtlsFingerprint.Read(sdp);

        Assert.Equal(certificate.Fingerprint, read);
        Assert.True(DtlsFingerprint.Matches(certificate.Encoded, read!));
        Assert.Equal(32, certificate.Fingerprint.Split(':').Length);
    }

    /// <summary>Runs both sides on their own threads and carries their datagrams across, as the network loop will.</summary>
    private static void Handshake(DtlsSrtpClient client, FakeDtlsServer server)
    {
        using var clientSide = new DtlsDatagramBridge(Limit);
        using var serverSide = new DtlsDatagramBridge(Limit);
        var accepted = Task.Run(() => new DtlsServerProtocol().Accept(server, serverSide));
        var connected = Task.Run(() => new DtlsClientProtocol().Connect(client, clientSide));
        var start = DateTime.UtcNow;

        while (!(accepted.IsCompleted && connected.IsCompleted) && !(accepted.IsFaulted || connected.IsFaulted) && DateTime.UtcNow - start < Deadline)
        {
            Carry(clientSide, serverSide);
            Carry(serverSide, clientSide);
            Thread.Yield();
        }

        clientSide.Close();
        serverSide.Close();
        Task.WaitAll(accepted, connected);
    }

    private static void Carry(DtlsDatagramBridge from, DtlsDatagramBridge to)
    {
        while (from.TryTakeOutgoing(out var datagram))
        {
            to.Deliver(datagram);
        }
    }
}
