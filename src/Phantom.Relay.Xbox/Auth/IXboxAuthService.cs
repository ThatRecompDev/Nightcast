// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>
/// The sign-in chain: Microsoft account, Xbox Live user token, XSTS for the streaming service, streaming token.
/// </summary>
public interface IXboxAuthService
{
    /// <summary>True when a refresh token is kept from an earlier sign-in.</summary>
    bool IsSignedIn { get; }

    /// <summary>The page the player signs in on.</summary>
    Uri SignInAddress { get; }

    /// <summary>Completes sign-in from the address the sign-in page sent the browser to.</summary>
    Task CompleteSignInAsync(Uri redirected, CancellationToken cancellation);

    /// <summary>A valid streaming token, renewing whatever in the chain expired.</summary>
    Task<StreamingToken> GetStreamingTokenAsync(CancellationToken cancellation);

    /// <summary>Forgets every token, the kept refresh token included.</summary>
    void SignOut();
}
