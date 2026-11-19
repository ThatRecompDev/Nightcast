// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.H264;

/// <summary>Takes whole, intact H.264 access units in Annex-B form, the moment each is complete.</summary>
public interface IAccessUnitSink
{
    /// <summary>One access unit; the span is valid only during the call.</summary>
    void Deliver(ReadOnlySpan<byte> accessUnit, uint timestamp, H264AccessUnitContent content);
}
