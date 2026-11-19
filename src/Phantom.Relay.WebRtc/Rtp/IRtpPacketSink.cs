// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>Takes RTP packets in sequence order from the reorder stage, each with its 64-bit sequence number.</summary>
public interface IRtpPacketSink
{
    /// <summary>One packet; the span is valid only during the call.</summary>
    void Deliver(ReadOnlySpan<byte> packet, long sequence);
}
