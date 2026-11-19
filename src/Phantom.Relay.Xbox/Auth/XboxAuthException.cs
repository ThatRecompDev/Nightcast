// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>Sign-in failed or the account cannot stream; the message says why in the player's words.</summary>
public sealed class XboxAuthException : Exception
{
    public XboxAuthException(string message)
        : base(message)
    {
    }
}
