// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Concurrent;
using Org.BouncyCastle.Tls;

namespace Phantom.Relay.WebRtc.Dtls;

/// <summary>
/// The datagram transport BouncyCastle's DTLS runs on, bridged to the frame-driven network loop. The loop hands every
/// DTLS datagram it receives to <see cref="Deliver"/> and sends what <see cref="TryTakeOutgoing"/> gives it, so all
/// socket calls stay on the loop's thread while the blocking handshake runs on its own.
/// </summary>
public sealed class DtlsDatagramBridge : DatagramTransport, IDisposable
{
    private const int NoDatagram = -1;

    private readonly ConcurrentQueue<byte[]> _incoming = new();
    private readonly ConcurrentQueue<byte[]> _outgoing = new();
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly int _limit;
    private volatile bool _closed;

    /// <param name="limit">The largest datagram either way: the path's payload size.</param>
    public DtlsDatagramBridge(int limit)
    {
        _limit = limit;
    }

    public bool IsClosed => _closed;

    /// <summary>
    /// True when a datagram waits. After the handshake the loop reads DTLS records only then, because BouncyCastle's
    /// DtlsTransport treats a wait of 0 ms as "wait forever", which would block the loop.
    /// </summary>
    public bool HasIncoming => !_incoming.IsEmpty;

    /// <summary>The loop's side: a DTLS datagram that arrived.</summary>
    public void Deliver(ReadOnlySpan<byte> datagram)
    {
        _incoming.Enqueue(datagram.ToArray());
        _arrived.Release();
    }

    /// <summary>The loop's side: the next datagram DTLS wants sent.</summary>
    public bool TryTakeOutgoing(out byte[] datagram)
    {
        return _outgoing.TryDequeue(out datagram!);
    }

    public int GetReceiveLimit()
    {
        return _limit;
    }

    public int GetSendLimit()
    {
        return _limit;
    }

    public int Receive(byte[] buf, int off, int len, int waitMillis)
    {
        return Receive(buf.AsSpan(off, len), waitMillis);
    }

    /// <summary>DTLS's side: waits up to <paramref name="waitMillis"/> for a datagram; −1 when none came or closed.</summary>
    public int Receive(Span<byte> buffer, int waitMillis)
    {
        if (_closed || !_arrived.Wait(waitMillis))
        {
            return NoDatagram;
        }

        if (!_incoming.TryDequeue(out var datagram))
        {
            return NoDatagram;
        }

        int length = Math.Min(datagram.Length, buffer.Length);
        datagram.AsSpan(0, length).CopyTo(buffer);

        return length;
    }

    public void Send(byte[] buf, int off, int len)
    {
        Send(buf.AsSpan(off, len));
    }

    public void Send(ReadOnlySpan<byte> buffer)
    {
        if (!_closed)
        {
            _outgoing.Enqueue(buffer.ToArray());
        }
    }

    public void Close()
    {
        _closed = true;
        _arrived.Release();
    }

    public void Dispose()
    {
        Close();
        _arrived.Dispose();
    }
}
