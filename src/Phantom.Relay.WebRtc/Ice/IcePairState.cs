// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>A candidate pair's check (RFC 8445 section 6.1.2.6).</summary>
public enum IcePairState
{
    Waiting,
    InProgress,
    Succeeded,
    Failed,
}
