// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>
/// A session's own self-signed ECDSA P-256 certificate, as WebRTC uses them (RFC 8827): nothing vouches for it; the
/// peer trusts it because its SHA-256 fingerprint travels in the signed-in signaling (a=fingerprint).
/// </summary>
public sealed class DtlsCertificate
{
    private const string SubjectName = "CN=WebRTC";
    private const string SignatureAlgorithm = "SHA256WITHECDSA";
    private const int SerialBits = 64;

    private static readonly TimeSpan Validity = TimeSpan.FromDays(30);

    private DtlsCertificate(AsymmetricKeyParameter privateKey, byte[] encoded)
    {
        PrivateKey = privateKey;
        Encoded = encoded;
        Fingerprint = DtlsFingerprint.Of(encoded);
    }

    public AsymmetricKeyParameter PrivateKey { get; }

    /// <summary>The DER encoding.</summary>
    public byte[] Encoded { get; }

    /// <summary>The SHA-256 fingerprint as SDP writes it: upper-case hex pairs joined by colons.</summary>
    public string Fingerprint { get; }

    /// <summary>A fresh key pair and certificate, valid from a day before <paramref name="now"/> for 30 days.</summary>
    public static DtlsCertificate Create(SecureRandom random, DateTimeOffset now)
    {
        var curve = ECNamedCurveTable.GetOid("P-256");
        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(curve, random));
        var keys = generator.GenerateKeyPair();

        var certificate = new X509V3CertificateGenerator();
        var name = new X509Name(SubjectName);
        certificate.SetSerialNumber(new BigInteger(SerialBits, random));
        certificate.SetIssuerDN(name);
        certificate.SetSubjectDN(name);
        certificate.SetPublicKey(keys.Public);

        certificate.SetNotBefore(now.UtcDateTime - TimeSpan.FromDays(1));
        certificate.SetNotAfter(now.UtcDateTime + Validity);
        var signed = certificate.Generate(new Asn1SignatureFactory(SignatureAlgorithm, keys.Private, random));

        return new DtlsCertificate(keys.Private, signed.GetEncoded());
    }

    /// <summary>Wraps a certificate and key made elsewhere (a test's, or one kept between sessions).</summary>
    public static DtlsCertificate From(AsymmetricKeyParameter privateKey, byte[] encoded)
    {
        return new DtlsCertificate(privateKey, encoded);
    }
}
