// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// What a live ICE run found: how it ended, the pair it selected, how long that took, how many candidates the console
/// gave, and what else arrived once connected, by the class of its first byte (RFC 7983): DTLS records (20 to 63),
/// RTP or RTCP (128 to 191), anything else.
/// </summary>
public sealed record IceOutcome(
    IceAgentState State,
    DatagramEndpoint? Selected,
    TimeSpan Elapsed,
    int RemoteCandidates,
    int DtlsDatagrams,
    int RtpDatagrams,
    int OtherDatagrams);
