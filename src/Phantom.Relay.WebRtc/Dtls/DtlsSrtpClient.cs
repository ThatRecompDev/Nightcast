// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>
/// The DTLS 1.2 client of a WebRTC session (RFC 5764, RFC 8827): ECDHE-ECDSA or ECDHE-RSA with AES-128-GCM, the
/// use_srtp extension offering AEAD-AES-128-GCM first and AES-CM-HMAC-SHA1-80 second, the peer authenticated by its
/// signaled fingerprint, and the SRTP keys exported once the handshake completes. Retransmits fast for a LAN.
/// </summary>
public sealed class DtlsSrtpClient : DefaultTlsClient
{
    /// <summary>A lost flight is resent after this long, doubling: a LAN answers in under a millisecond.</summary>
    private const int ResendMillis = 100;

    private const int HandshakeTimeoutMillis = 10_000;

    private readonly BcTlsCrypto _crypto;
    private readonly DtlsCertificate _certificate;
    private readonly string _expectedFingerprint;

    public DtlsSrtpClient(BcTlsCrypto crypto, DtlsCertificate certificate, string expectedFingerprint)
        : base(crypto)
    {
        _crypto = crypto;
        _certificate = certificate;
        _expectedFingerprint = expectedFingerprint;
    }

    /// <summary>The profile the server chose, once it answered.</summary>
    public SrtpProfile? Profile { get; private set; }

    /// <summary>The exported keys, once the handshake completed.</summary>
    public SrtpKeys? Keys { get; private set; }

    /// <summary>The negotiated cipher suite (IANA number), once the handshake completed.</summary>
    public int CipherSuite { get; private set; }

    public override TlsAuthentication GetAuthentication()
    {
        return new DtlsPeerAuthentication(m_context, _crypto, _certificate, _expectedFingerprint);
    }

    public override IDictionary<int, byte[]> GetClientExtensions()
    {
        var extensions = TlsExtensionsUtilities.EnsureExtensionsInitialised(base.GetClientExtensions());
        int[] profiles = [SrtpProtectionProfile.SRTP_AEAD_AES_128_GCM, SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80];
        TlsSrtpUtilities.AddUseSrtpExtension(extensions, new UseSrtpData(profiles, []));

        return extensions;
    }

    public override void ProcessServerExtensions(IDictionary<int, byte[]> serverExtensions)
    {
        base.ProcessServerExtensions(serverExtensions);
        var chosen = TlsSrtpUtilities.GetUseSrtpExtension(serverExtensions);

        if (chosen is null || chosen.ProtectionProfiles.Length != 1)
        {
            throw new TlsFatalAlert(AlertDescription.illegal_parameter);
        }

        Profile = ToProfile(chosen.ProtectionProfiles[0]);
    }

    public override void NotifyHandshakeComplete()
    {
        base.NotifyHandshakeComplete();
        var profile = Profile ?? throw new TlsFatalAlert(AlertDescription.internal_error);
        byte[] material = m_context.ExportKeyingMaterial(SrtpKeys.ExporterLabel, null, SrtpKeys.MaterialLength(profile));
        Keys = SrtpKeys.Split(profile, material);
        CipherSuite = m_context.SecurityParameters.CipherSuite;
    }

    public override int GetHandshakeResendTimeMillis()
    {
        return ResendMillis;
    }

    public override int GetHandshakeTimeoutMillis()
    {
        return HandshakeTimeoutMillis;
    }

    protected override ProtocolVersion[] GetSupportedVersions()
    {
        return ProtocolVersion.DTLSv12.Only();
    }

    protected override int[] GetSupportedCipherSuites()
    {
        return
        [
            Org.BouncyCastle.Tls.CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
            Org.BouncyCastle.Tls.CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        ];
    }

    private static SrtpProfile ToProfile(int profile)
    {
        switch (profile)
        {
            case SrtpProtectionProfile.SRTP_AEAD_AES_128_GCM:
                return SrtpProfile.AeadAes128Gcm;
            case SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80:
                return SrtpProfile.AesCm128HmacSha1_80;
            default:
                throw new TlsFatalAlert(AlertDescription.illegal_parameter);
        }
    }
}
