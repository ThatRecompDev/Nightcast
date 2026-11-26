// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>
/// A TURN server over UDP and the long-term credential it takes (RFC 8489 section 9.2): short-lived ones, such as
/// Cloudflare's, work the same way. The head finds it and hands it to the stream; the password is never logged.
/// </summary>
public sealed record TurnServer(DatagramEndpoint Endpoint, string Username, string Password)
{
    /// <summary>Without the password, for logs.</summary>
    public override string ToString()
    {
        return $"TURN server at port {Endpoint.Port}";
    }
}
