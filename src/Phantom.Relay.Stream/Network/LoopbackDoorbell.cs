// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.Stream.Network;

/// <summary>
/// Wakes the network thread by sending one byte from a socket of its own to the stream's socket on the loopback
/// address. The byte, 0, is neither STUN, DTLS nor RTP (RFC 7983's first-byte classes), so the stream drops it unread;
/// its only effect is that the stream's wait for a datagram returns. The doorbell's socket is its own because the
/// stream's transport is the network thread's alone.
/// </summary>
public sealed class LoopbackDoorbell : IStreamWaker
{
    private const byte Loopback = 127;

    private readonly IDatagramTransport _bell;
    private readonly DatagramEndpoint _stream;
    private readonly byte[] _ring = [0];

    /// <param name="bell">A socket of the doorbell's own, used by nothing else.</param>
    /// <param name="streamPort">The port the stream's socket is bound to (on any address).</param>
    public LoopbackDoorbell(IDatagramTransport bell, ushort streamPort)
    {
        _bell = bell;
        _stream = DatagramEndpoint.FromOctets(Loopback, 0, 0, 1, streamPort);
    }

    [HotPath]
    public void Wake()
    {
        _bell.TrySend(_stream, _ring);
    }
}
