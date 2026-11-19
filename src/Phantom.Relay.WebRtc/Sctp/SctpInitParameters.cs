// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>What an INIT or INIT ACK announces: its sender's tag, window, streams, first TSN and whether it supports FORWARD TSN.</summary>
internal readonly record struct SctpInitParameters(
    uint InitiateTag,
    uint Window,
    ushort OutboundStreams,
    ushort InboundStreams,
    uint InitialTsn,
    bool ForwardTsn);
