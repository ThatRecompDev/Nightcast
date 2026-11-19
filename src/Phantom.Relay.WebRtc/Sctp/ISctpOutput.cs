// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// Where an association's packets go: DTLS application data in WebRTC (RFC 8261), a fake link in tests. The packet
/// is the association's buffer and valid only during the call, so an implementation copies what it keeps.
/// </summary>
public interface ISctpOutput
{
    void Send(ReadOnlySpan<byte> packet);
}
