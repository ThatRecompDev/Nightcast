// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The data channels of one association (RFC 8831, RFC 8832): <see cref="Open"/> sends DATA_CHANNEL_OPEN on this side's
/// next stream (even as the DTLS client, odd as the server); the peer's OPEN is answered with DATA_CHANNEL_ACK and its
/// channel waits in <see cref="TryTakeOpenedByPeer"/>. <see cref="TryReceive"/>, polled every frame, handles the DCEP
/// messages and returns the channels' own messages, string or binary, empty ones included. Opening a channel
/// allocates; sending and receiving do not.
/// </summary>
public sealed class DataChannelHub
{
    /// <summary>Channels the peer opened that wait to be taken.</summary>
    private const int MaxWaitingChannels = 64;

    private const int StreamParity = 2;

    private readonly SctpAssociation _association;
    private readonly DataChannel?[] _channels;
    private readonly Queue<DataChannel> _openedByPeer = new(MaxWaitingChannels);
    private readonly int _ownParity;
    private int _nextId;

    public DataChannelHub(SctpAssociation association, DataChannelSide side)
    {
        _association = association;
        _channels = new DataChannel?[association.Streams];
        _ownParity = side == DataChannelSide.DtlsClient ? 0 : 1;
        _nextId = _ownParity;
    }

    /// <summary>
    /// Opens a channel on this side's next free stream: sends DATA_CHANNEL_OPEN, ordered and reliable. The channel
    /// is <see cref="DataChannelState.Opening"/> until the ACK arrives, and can already carry messages.
    /// </summary>
    public DataChannel Open(DataChannelOptions options)
    {
        if (_association.State != SctpAssociationState.Established)
        {
            throw new InvalidOperationException("channels open once the association is established");
        }

        int id = _nextId;

        while (id < _association.OutboundStreams && _channels[id] is not null)
        {
            id += StreamParity;
        }

        if (id >= _association.OutboundStreams)
        {
            throw new InvalidOperationException("no stream is left for another channel");
        }

        var channel = new DataChannel((ushort)id, options, DataChannelState.Opening);

        if (!_association.Send(channel.Id, DataChannelProtocol.ControlPayload, DataChannelProtocol.Open(options), SctpSendPolicy.Reliable))
        {
            throw new InvalidOperationException("the association's send buffer is full");
        }

        _channels[id] = channel;
        _nextId = id + StreamParity;

        return channel;
    }

    /// <summary>The next channel the peer opened, already open.</summary>
    public bool TryTakeOpenedByPeer([NotNullWhen(true)] out DataChannel? channel)
    {
        return _openedByPeer.TryDequeue(out channel);
    }

    /// <summary>Sends a UTF-8 string message; false when the association cannot take it now.</summary>
    [HotPath]
    public bool SendString(DataChannel channel, ReadOnlySpan<byte> utf8)
    {
        return Send(channel, utf8, DataChannelProtocol.StringPayload, DataChannelProtocol.EmptyStringPayload);
    }

    /// <summary>Sends a binary message; false when the association cannot take it now.</summary>
    [HotPath]
    public bool SendBinary(DataChannel channel, ReadOnlySpan<byte> payload)
    {
        return Send(channel, payload, DataChannelProtocol.BinaryPayload, DataChannelProtocol.EmptyBinaryPayload);
    }

    /// <summary>
    /// Handles waiting DCEP messages and returns the oldest channel message, which stays readable until
    /// <see cref="Release"/>. Messages on a stream with no channel are dropped.
    /// </summary>
    [HotPath]
    public bool TryReceive([NotNullWhen(true)] out DataChannel? channel, out bool isString, out ReadOnlySpan<byte> payload)
    {
        while (_association.TryPeek(out ushort stream, out uint protocol, out var message))
        {
            var found = _channels[stream];

            if (protocol == DataChannelProtocol.ControlPayload || found is null || !IsChannelMessage(protocol))
            {
                HandleControl(stream, protocol, message);
                _association.Release();
                continue;
            }

            channel = found;
            isString = protocol is DataChannelProtocol.StringPayload or DataChannelProtocol.EmptyStringPayload;
            payload = message;

            if (protocol is DataChannelProtocol.EmptyStringPayload or DataChannelProtocol.EmptyBinaryPayload)
            {
                payload = ReadOnlySpan<byte>.Empty;
            }

            return true;
        }

        channel = null;
        isString = false;
        payload = default;

        return false;
    }

    /// <summary>Frees the message <see cref="TryReceive"/> returned.</summary>
    [HotPath]
    public void Release()
    {
        _association.Release();
    }

    [HotPath]
    private bool Send(DataChannel channel, ReadOnlySpan<byte> payload, uint protocol, uint emptyProtocol)
    {
        var policy = channel.State == DataChannelState.Opening ? channel.OpeningPolicy : channel.Policy;

        if (payload.IsEmpty)
        {
            return _association.Send(channel.Id, emptyProtocol, EmptyMessage, policy);
        }

        return _association.Send(channel.Id, protocol, payload, policy);
    }

    private static bool IsChannelMessage(uint protocol)
    {
        return protocol is DataChannelProtocol.StringPayload
            or DataChannelProtocol.BinaryPayload
            or DataChannelProtocol.EmptyStringPayload
            or DataChannelProtocol.EmptyBinaryPayload;
    }

    private void HandleControl(ushort stream, uint protocol, ReadOnlySpan<byte> message)
    {
        if (protocol != DataChannelProtocol.ControlPayload || message.IsEmpty)
        {
            return;
        }

        if (message[0] == DataChannelProtocol.OpenMessage)
        {
            AcceptOpen(stream, message);
        }
        else if (message[0] == DataChannelProtocol.AckMessage)
        {
            var channel = _channels[stream];

            if (channel is not null)
            {
                channel.State = DataChannelState.Open;
            }
        }
    }

    /// <summary>The peer's OPEN on one of its own streams: the channel opens at once and the ACK goes back on its stream.</summary>
    private void AcceptOpen(ushort stream, ReadOnlySpan<byte> message)
    {
        if (stream % StreamParity == _ownParity || _channels[stream] is not null || _openedByPeer.Count == MaxWaitingChannels)
        {
            return;
        }

        if (!DataChannelProtocol.TryReadOpen(message, out var options))
        {
            return;
        }

        var channel = new DataChannel(stream, options, DataChannelState.Open);
        _channels[stream] = channel;
        _openedByPeer.Enqueue(channel);
        _association.Send(stream, DataChannelProtocol.ControlPayload, AckMessage, SctpSendPolicy.Reliable);
    }

    private static ReadOnlySpan<byte> EmptyMessage => [0];

    private static ReadOnlySpan<byte> AckMessage => [DataChannelProtocol.AckMessage];
}
