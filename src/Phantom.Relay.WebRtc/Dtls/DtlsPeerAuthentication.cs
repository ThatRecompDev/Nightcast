// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>
/// WebRTC's authentication (RFC 8827): the peer's certificate is accepted only when its fingerprint is the one its
/// signaled description carried; our own certificate answers the peer's certificate request.
/// </summary>
public sealed class DtlsPeerAuthentication : TlsAuthentication
{
    private readonly TlsContext _context;
    private readonly BcTlsCrypto _crypto;
    private readonly DtlsCertificate _own;
    private readonly string _expectedFingerprint;

    public DtlsPeerAuthentication(TlsContext context, BcTlsCrypto crypto, DtlsCertificate own, string expectedFingerprint)
    {
        _context = context;
        _crypto = crypto;
        _own = own;
        _expectedFingerprint = expectedFingerprint;
    }

    public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
    {
        var chain = serverCertificate.Certificate;

        if (chain is null || chain.IsEmpty || !DtlsFingerprint.Matches(chain.GetCertificateAt(0).GetEncoded(), _expectedFingerprint))
        {
            throw new TlsFatalAlert(AlertDescription.bad_certificate);
        }
    }

    public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
    {
        var chain = new Certificate([new BcTlsCertificate(_crypto, _own.Encoded)]);
        var algorithm = SignatureAndHashAlgorithm.GetInstance(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa);

        return new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(_context), _crypto, _own.PrivateKey, chain, algorithm);
    }
}
