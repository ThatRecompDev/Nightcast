// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Network;

/// <summary>
/// Wakes the network thread from its wait for a datagram, so a pad change from another thread goes out at once
/// instead of waiting for the next datagram or the wait's timeout (a Windows timer tick, ~15.6 ms).
/// </summary>
public interface IStreamWaker
{
    void Wake();
}
