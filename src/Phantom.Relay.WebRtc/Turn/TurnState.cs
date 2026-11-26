// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>Where a TURN client's allocation stands.</summary>
public enum TurnState
{
    Idle,
    Allocating,
    Allocated,
    Failed,
}
