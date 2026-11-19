// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Dtls;

/// <summary>
/// A DTLS-SRTP server standing in for the console: ECDSA certificate, use_srtp answering with the client's first
/// profile, a certificate request, and the keys exported like the client's.
/// </summary>
public sealed class FakeDtlsServer : DefaultTlsServer
{
    private readonly BcTlsCrypto _crypto;
    private readonly DtlsCertificate _certificate;
    private int _profile;

    public FakeDtlsServer(BcTlsCrypto crypto, DtlsCertificate certificate)
        : base(crypto)
    {
        _crypto = crypto;
        _certificate = certificate;
    }

    public byte[]? ClientCertificate { get; private set; }

    public byte[]? Material { get; private set; }

    public override void ProcessClientExtensions(IDictionary<int, byte[]> clientExtensions)
    {
        base.ProcessClientExtensions(clientExtensions);
        _profile = TlsSrtpUtilities.GetUseSrtpExtension(clientExtensions).ProtectionProfiles[0];
    }

    public override IDictionary<int, byte[]> GetServerExtensions()
    {
        var extensions = TlsExtensionsUtilities.EnsureExtensionsInitialised(base.GetServerExtensions());
        TlsSrtpUtilities.AddUseSrtpExtension(extensions, new UseSrtpData([_profile], []));

        return extensions;
    }

    public override CertificateRequest GetCertificateRequest()
    {
        var algorithms = new List<SignatureAndHashAlgorithm> { SignatureAndHashAlgorithm.GetInstance(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa) };

        return new CertificateRequest([ClientCertificateType.ecdsa_sign], algorithms, null);
    }

    public override void NotifyClientCertificate(Certificate clientCertificate)
    {
        ClientCertificate = clientCertificate.GetCertificateAt(0).GetEncoded();
    }

    public override void NotifyHandshakeComplete()
    {
        base.NotifyHandshakeComplete();
        Material = m_context.ExportKeyingMaterial(SrtpKeyingMaterial.ExporterLabel, null, SrtpKeyingMaterial.ExporterLength(SrtpProfile.AeadAes128Gcm));
    }

    protected override ProtocolVersion[] GetSupportedVersions()
    {
        return ProtocolVersion.DTLSv12.Only();
    }

    protected override int[] GetSupportedCipherSuites()
    {
        return [CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256];
    }

    protected override TlsCredentialedSigner GetECDsaSignerCredentials()
    {
        var chain = new Certificate([new BcTlsCertificate(_crypto, _certificate.Encoded)]);
        var algorithm = SignatureAndHashAlgorithm.GetInstance(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa);

        return new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(m_context), _crypto, _certificate.PrivateKey, chain, algorithm);
    }
}
