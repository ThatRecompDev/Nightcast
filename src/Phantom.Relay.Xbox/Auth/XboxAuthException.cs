// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>Sign-in failed or the account cannot stream; the message says why in the player's words.</summary>
public sealed class XboxAuthException : Exception
{
    public XboxAuthException(string message)
        : base(message)
    {
    }

    /// <summary>True when the kept sign-in is no longer accepted and was forgotten: signing in again fixes it.</summary>
    public bool SignInAgain { get; init; }
}
