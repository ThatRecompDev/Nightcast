// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Collections;
using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Tests.Turn;

/// <summary>A transport whose datagrams to one endpoint are lost: a NAT that drops the direct path.</summary>
public sealed class BlockingTransport : IDatagramTransport
{
    private readonly IDatagramTransport _inner;
    private readonly DatagramEndpoint _blocked;

    public BlockingTransport(IDatagramTransport inner, DatagramEndpoint blocked)
    {
        _inner = inner;
        _blocked = blocked;
    }

    public DatagramEndpoint LocalEndpoint => _inner.LocalEndpoint;

    public int MaxDatagramLength => _inner.MaxDatagramLength;

    public bool TrySend(DatagramEndpoint destination, ReadOnlySpan<byte> datagram)
    {
        return destination != _blocked && _inner.TrySend(destination, datagram);
    }

    public int Receive(ByteRing destination)
    {
        return _inner.Receive(destination);
    }
}
