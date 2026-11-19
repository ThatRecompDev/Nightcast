// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>The streaming service refused or failed a session step.</summary>
public sealed class XHomeSessionException : Exception
{
    public XHomeSessionException(string message)
        : base(message)
    {
    }
}
