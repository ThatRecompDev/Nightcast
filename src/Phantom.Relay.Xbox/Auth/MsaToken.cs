// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>Microsoft account tokens. Only the refresh token is kept between runs.</summary>
public sealed record MsaToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    public bool IsExpired(TimeProvider clock)
    {
        return clock.GetUtcNow() >= ExpiresAt - XboxEndpoints.ExpiryMargin;
    }
}
