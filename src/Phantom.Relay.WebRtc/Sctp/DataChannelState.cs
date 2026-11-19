// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>Where a data channel is.</summary>
public enum DataChannelState
{
    /// <summary>DATA_CHANNEL_OPEN sent, no ACK yet; messages may already go, ordered, as RFC 8832 requires.</summary>
    Opening,

    Open,
}
