// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>Where a session's provisioning is.</summary>
public enum SessionState
{
    Unknown,
    WaitingForResources,
    Provisioning,
    ReadyToConnect,
    Provisioned,
    Failed,
}
