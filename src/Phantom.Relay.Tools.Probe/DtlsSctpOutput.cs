// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Org.BouncyCastle.Tls;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.Tools.Probe;

/// <summary>SCTP packets go out as DTLS application data (RFC 8261).</summary>
public sealed class DtlsSctpOutput : ISctpOutput
{
    private readonly DtlsTransport _dtls;

    public DtlsSctpOutput(DtlsTransport dtls)
    {
        _dtls = dtls;
    }

    public void Send(ReadOnlySpan<byte> packet)
    {
        _dtls.Send(packet);
    }
}
