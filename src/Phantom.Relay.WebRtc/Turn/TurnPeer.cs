// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>
/// A peer the relay talks to through a channel of its own (RFC 8656 section 12): binding the channel also installs the
/// peer's permission, and data then travels with a four-byte header instead of a STUN indication's thirty-six.
/// </summary>
public sealed class TurnPeer
{
    public TurnPeer(ushort channel)
    {
        Channel = channel;
    }

    public ushort Channel { get; }

    public DatagramEndpoint Endpoint { get; set; }

    /// <summary>True once the server confirmed the binding: data may go out on the channel.</summary>
    public bool Bound { get; set; }

    /// <summary>True when the server refused the binding (a peer address it forbids, such as a private one).</summary>
    public bool Refused { get; set; }

    /// <summary>When the binding is renewed (<see cref="TimeProvider.GetTimestamp"/> ticks).</summary>
    public long RebindAt { get; set; }

    public TurnRequest Request { get; } = new();
}
