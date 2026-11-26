// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Phantom.Engine.Core.Collections;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.InMemory;
using Phantom.Relay.WebRtc.Stun;
using Phantom.Relay.WebRtc.Turn;

namespace Phantom.Relay.WebRtc.Tests.Turn;

/// <summary>
/// A TURN server as RFC 8656 describes one, on the in-memory network: it challenges an allocation with 401, grants it
/// to the long-term credential, binds channels (refusing forbidden peers with 403), and relays ChannelData both ways
/// between the client and peers through its relayed address.
/// </summary>
public sealed class FakeTurnServer
{
    public const string Realm = "relay.example";
    public const string Nonce = "nonce-0001";
    public const uint LifetimeSeconds = 600;

    private readonly IDatagramTransport _control;
    private readonly IDatagramTransport _relay;
    private readonly StunIntegrity _integrity;
    private readonly ByteRing _ring = new(ByteRing.CapacityFor(DatagramFrame.FrameLength(1500)));
    private readonly byte[] _send = new byte[1500];
    private readonly Dictionary<ushort, DatagramEndpoint> _channels = [];

    private DatagramEndpoint? _client;

    public FakeTurnServer(InMemoryDatagramNetwork network, DatagramEndpoint at, DatagramEndpoint relayAt, string username, string password)
    {
        _control = network.CreateTransport(at);
        _relay = network.CreateTransport(relayAt);
        _integrity = new StunIntegrity(Key(username, password));
    }

    public DatagramEndpoint RelayAt => _relay.LocalEndpoint;

    /// <summary>Peer addresses whose channel is refused, as a server refuses private addresses.</summary>
    public HashSet<uint> Forbidden { get; } = [];

    public int Challenges { get; private set; }

    public int Refreshes { get; private set; }

    public int ChannelDataRelayed { get; private set; }

    public void Drain()
    {
        _control.Receive(_ring);

        while (_ring.TryPeek(out var frame))
        {
            Control(DatagramFrame.Source(frame), DatagramFrame.Payload(frame));
            _ring.Release();
        }

        _relay.Receive(_ring);

        while (_ring.TryPeek(out var frame))
        {
            FromPeer(DatagramFrame.Source(frame), DatagramFrame.Payload(frame));
            _ring.Release();
        }
    }

    private static byte[] Key(string username, string password)
    {
        byte[] text = Encoding.UTF8.GetBytes($"{username}:{Realm}:{password}");
        var digest = new MD5Digest();
        byte[] key = new byte[digest.GetDigestSize()];
        digest.BlockUpdate(text, 0, text.Length);
        digest.DoFinal(key, 0);

        return key;
    }

    private void Control(DatagramEndpoint source, ReadOnlySpan<byte> message)
    {
        if ((message[0] & 0xF0) == 0x40)
        {
            ushort channel = BinaryPrimitives.ReadUInt16BigEndian(message);
            int length = BinaryPrimitives.ReadUInt16BigEndian(message[2..]);

            if (_channels.TryGetValue(channel, out var peer))
            {
                ChannelDataRelayed++;
                _relay.TrySend(peer, message.Slice(TurnFormat.ChannelHeaderLength, length));
            }

            return;
        }

        if (!StunReader.IsMessage(message))
        {
            return;
        }

        if (!StunReader.HasValidIntegrity(message, _integrity))
        {
            Challenges++;
            Error(source, message, 4, 1, StunReader.Type(message) | 0x0110);

            return;
        }

        switch (StunReader.Type(message))
        {
            case TurnFormat.AllocateRequest:
                _client = source;
                Success(source, message, TurnFormat.AllocateSuccess, source);

                return;
            case TurnFormat.RefreshRequest:
                Refreshes++;
                Success(source, message, TurnFormat.RefreshSuccess, null);

                return;
            case TurnFormat.ChannelBindRequest:
                Bind(source, message);

                return;
        }
    }

    private void Bind(DatagramEndpoint source, ReadOnlySpan<byte> message)
    {
        StunReader.TryFind(message, TurnFormat.ChannelNumber, out var number, out _);
        StunReader.TryFind(message, TurnFormat.XorPeerAddress, out var address, out _);
        StunReader.TryReadXorMappedAddress(address, out var peer);

        if (Forbidden.Contains(peer.Address))
        {
            Error(source, message, 4, 3, TurnFormat.ChannelBindError);

            return;
        }

        _channels[BinaryPrimitives.ReadUInt16BigEndian(number)] = peer;
        var writer = new StunWriter(_send, TurnFormat.ChannelBindSuccess, StunReader.TransactionId(message));
        writer.AddIntegrity(_integrity);
        _control.TrySend(source, writer.Written);
    }

    private void FromPeer(DatagramEndpoint source, ReadOnlySpan<byte> data)
    {
        var client = _client;

        foreach (var (channel, peer) in _channels)
        {
            if (peer == source && client is not null)
            {
                BinaryPrimitives.WriteUInt16BigEndian(_send, channel);
                BinaryPrimitives.WriteUInt16BigEndian(_send.AsSpan(2), (ushort)data.Length);
                data.CopyTo(_send.AsSpan(TurnFormat.ChannelHeaderLength));
                _control.TrySend(client.Value, _send.AsSpan(0, TurnFormat.ChannelHeaderLength + data.Length));

                return;
            }
        }
    }

    private void Success(DatagramEndpoint source, ReadOnlySpan<byte> request, ushort type, DatagramEndpoint? mapped)
    {
        var writer = new StunWriter(_send, type, StunReader.TransactionId(request));

        if (mapped is not null)
        {
            writer.AddXorAddress(TurnFormat.XorRelayedAddress, RelayAt);
            writer.AddXorMappedAddress(mapped.Value);
        }

        writer.AddUInt32(TurnFormat.Lifetime, LifetimeSeconds);
        writer.AddIntegrity(_integrity);
        writer.AddFingerprint();
        _control.TrySend(source, writer.Written);
    }

    private void Error(DatagramEndpoint source, ReadOnlySpan<byte> request, byte errorClass, byte number, int type)
    {
        var writer = new StunWriter(_send, (ushort)type, StunReader.TransactionId(request));
        writer.AddUInt32(StunFormat.ErrorCode, ((uint)errorClass << 8) | number);
        writer.Add(TurnFormat.Realm, Encoding.UTF8.GetBytes(Realm));
        writer.Add(TurnFormat.Nonce, Encoding.UTF8.GetBytes(Nonce));
        writer.AddFingerprint();
        _control.TrySend(source, writer.Written);
    }
}
