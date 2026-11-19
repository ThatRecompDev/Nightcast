// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// This side's DTLS role, which picks the stream identifiers of the channels it opens (RFC 8832 section 6): the DTLS
/// client takes even ones, the server odd ones, so both can open channels at once without colliding.
/// </summary>
public enum DataChannelSide
{
    /// <summary>Even stream identifiers.</summary>
    DtlsClient,

    /// <summary>Odd stream identifiers.</summary>
    DtlsServer,
}
