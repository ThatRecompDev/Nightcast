// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// What a live ICE run found: how it ended, the pair it selected, how long that took, the path's round trip on the
/// first answer and on the last keep-alive, how many candidates the console gave, and what else arrived once
/// connected, by the class of its first byte (RFC 7983): DTLS records (20 to 63), RTP or RTCP (128 to 191), anything
/// else.
/// </summary>
public sealed record IceOutcome(
    IceAgentState State,
    DatagramEndpoint? Selected,
    TimeSpan Elapsed,
    TimeSpan? FirstRoundTrip,
    TimeSpan? LatestRoundTrip,
    int RemoteCandidates,
    int DtlsDatagrams,
    int RtpDatagrams,
    int OtherDatagrams)
{
    /// <summary>How the DTLS handshake ended: "not run", "connected", "timed out" or the failure.</summary>
    public string DtlsResult { get; init; } = "not run";

    public TimeSpan? DtlsElapsed { get; init; }

    public string? SrtpProfile { get; init; }

    public int CipherSuite { get; init; }

    /// <summary>The data channels, when DTLS connected.</summary>
    public ChannelReport? Channels { get; init; }
}
