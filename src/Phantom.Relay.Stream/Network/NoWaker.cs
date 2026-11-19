// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Network;

/// <summary>For a transport the network thread polls rather than waits on (tests, the in-memory network): nothing to wake.</summary>
public sealed class NoWaker : IStreamWaker
{
    public void Wake()
    {
    }
}
