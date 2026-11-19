// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Collections;
using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.Stream.Tests.Fakes;

/// <summary>A transport that sends into nothing and never receives: the console's side is never reached.</summary>
public sealed class SilentTransport : IDatagramTransport
{
    public DatagramEndpoint LocalEndpoint { get; } = DatagramEndpoint.FromOctets(127, 0, 0, 1, 50000);

    public int MaxDatagramLength => 2048;

    public int Sent { get; private set; }

    public bool TrySend(DatagramEndpoint destination, ReadOnlySpan<byte> datagram)
    {
        Sent++;

        return true;
    }

    public int Receive(ByteRing destination)
    {
        return 0;
    }
}
