// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>Reads the service's state names.</summary>
public static class SessionStates
{
    public static SessionState Parse(string? name)
    {
        switch (name)
        {
            case "WaitingForResources":
                return SessionState.WaitingForResources;
            case "Provisioning":
                return SessionState.Provisioning;
            case "ReadyToConnect":
                return SessionState.ReadyToConnect;
            case "Provisioned":
                return SessionState.Provisioned;
            case "Failed":
                return SessionState.Failed;
            default:
                return SessionState.Unknown;
        }
    }

    /// <summary>True once the session takes an SDP offer.</summary>
    public static bool IsReady(SessionState state)
    {
        return state == SessionState.ReadyToConnect || state == SessionState.Provisioned;
    }
}
