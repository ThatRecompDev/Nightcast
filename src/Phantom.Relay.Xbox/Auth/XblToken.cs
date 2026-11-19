// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>An Xbox Live user or XSTS token with the user hash claim.</summary>
public sealed record XblToken(string Token, string UserHash, DateTimeOffset NotAfter)
{
    public bool IsExpired(TimeProvider clock)
    {
        return clock.GetUtcNow() >= NotAfter - XboxEndpoints.ExpiryMargin;
    }
}
