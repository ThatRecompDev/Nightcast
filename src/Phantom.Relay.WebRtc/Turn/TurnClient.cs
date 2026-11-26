// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Stun;

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>
/// A TURN client over UDP (RFC 8656) as a pure state machine on the transport ICE uses, so the relayed address and our
/// host address share one socket: <see cref="Start"/> asks for an allocation (answering the server's 401 challenge
/// with the long-term credential), <see cref="Service"/> every turn retransmits and keeps the allocation and channels
/// alive, <see cref="TryHandle"/> takes every datagram from the server, and <see cref="Bind"/> gives a peer a channel
/// (which also installs its permission) so <see cref="TrySend"/> can reach it. Data travels as ChannelData both ways;
/// nothing is allocated per datagram.
/// </summary>
public sealed class TurnClient
{
    /// <summary>How many peers the relay reaches: every console candidate and the few addresses learned from checks.</summary>
    public const int MaxPeers = 16;

    /// <summary>REALM and NONCE are at most 763 bytes (RFC 8489 sections 14.9 and 14.10).</summary>
    private const int MaxTextLength = 763;

    /// <summary>A ChannelData message's first byte is 0x40 to 0x4F (RFC 7983's TURN channel range).</summary>
    private const int ChannelByteMask = 0xF0;

    private const int ChannelByte = 0x40;

    private const int LengthOffset = 2;
    private const int HalvingShift = 1;
    private const char Separator = ':';

    private readonly IDatagramTransport _transport;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly TurnServer _server;
    private readonly byte[] _send;
    private readonly byte[] _username;
    private readonly byte[] _realm = new byte[MaxTextLength];
    private readonly byte[] _nonce = new byte[MaxTextLength];
    private readonly TurnRequest _allocation = new();
    private readonly TurnPeer[] _peers = new TurnPeer[MaxPeers];

    private int _realmLength;
    private int _nonceLength;
    private int _peerCount;
    private StunIntegrity? _integrity;
    private bool _refreshing;
    private long _refreshAt;

    public TurnClient(IDatagramTransport transport, TimeProvider clock, IRandomSource random, TurnServer server)
    {
        _transport = transport;
        _clock = clock;
        _random = random;
        _server = server;

        // The buffers for every message, and a channel for each peer, made now so nothing is made per datagram.
        _send = new byte[transport.MaxDatagramLength];
        _username = Encoding.UTF8.GetBytes(server.Username);

        for (int index = 0; index < _peers.Length; index++)
        {
            _peers[index] = new TurnPeer((ushort)(TurnFormat.FirstChannel + index));
        }
    }

    public TurnState State { get; private set; }

    /// <summary>Our address on the relay, once allocated: the relayed candidate the console checks.</summary>
    public DatagramEndpoint? Relayed { get; private set; }

    /// <summary>Our address as the server saw it, once allocated: our NAT's mapping, a server-reflexive candidate.</summary>
    public DatagramEndpoint? Mapped { get; private set; }

    /// <summary>The error code that ended the allocation (401: the credential was refused), or 0.</summary>
    public int Refusal { get; private set; }

    public DatagramEndpoint Server => _server.Endpoint;

    /// <summary>Asks for the allocation; the server's challenge and the answer to it follow through <see cref="TryHandle"/>.</summary>
    public void Start()
    {
        State = TurnState.Allocating;
        _refreshing = false;
        Begin(_allocation, _clock.GetTimestamp());
        SendAllocation();
    }

    /// <summary>Retransmits what is due and renews the allocation and the channels before they lapse.</summary>
    [HotPath]
    public void Service()
    {
        long now = _clock.GetTimestamp();

        if (State == TurnState.Allocating || State == TurnState.Allocated)
        {
            ServiceAllocation(now);
        }

        if (State == TurnState.Allocated)
        {
            ServicePeers(now);
        }
    }

    /// <summary>
    /// Gives <paramref name="peer"/> a channel, bound now when allocated or as soon as the allocation is; false when
    /// every channel is taken.
    /// </summary>
    public bool Bind(DatagramEndpoint peer)
    {
        if (PeerAt(peer) is not null)
        {
            return true;
        }

        if (_peerCount == _peers.Length)
        {
            return false;
        }

        var added = _peers[_peerCount++];
        added.Endpoint = peer;
        added.Bound = false;
        added.Refused = false;
        added.RebindAt = 0;

        if (State == TurnState.Allocated)
        {
            Begin(added.Request, _clock.GetTimestamp());
            SendChannelBind(added);
        }

        return true;
    }

    /// <summary>True once <paramref name="peer"/>'s channel is bound, so <see cref="TrySend"/> reaches it.</summary>
    public bool IsBound(DatagramEndpoint peer)
    {
        return PeerAt(peer)?.Bound == true;
    }

    /// <summary>True when the server refused <paramref name="peer"/>'s channel (an address it forbids, such as a private one).</summary>
    public bool IsRefused(DatagramEndpoint peer)
    {
        return PeerAt(peer)?.Refused == true;
    }

    /// <summary>Sends <paramref name="data"/> to <paramref name="peer"/> through the relay; false until its channel is bound.</summary>
    [HotPath]
    public bool TrySend(DatagramEndpoint peer, ReadOnlySpan<byte> data)
    {
        var bound = PeerAt(peer);

        if (bound is null || !bound.Bound || TurnFormat.ChannelHeaderLength + data.Length > _send.Length)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt16BigEndian(_send, bound.Channel);
        BinaryPrimitives.WriteUInt16BigEndian(_send.AsSpan(LengthOffset), (ushort)data.Length);
        data.CopyTo(_send.AsSpan(TurnFormat.ChannelHeaderLength));

        return _transport.TrySend(_server.Endpoint, _send.AsSpan(0, TurnFormat.ChannelHeaderLength + data.Length));
    }

    /// <summary>
    /// Takes a datagram when it came from the server (false for any other source, which the caller handles). A peer's
    /// data relayed to us comes back as <paramref name="peer"/> and <paramref name="data"/> (a slice of the datagram);
    /// for the server's own answers <paramref name="data"/> is empty.
    /// </summary>
    [HotPath]
    public bool TryHandle(DatagramEndpoint source, ReadOnlySpan<byte> datagram, out DatagramEndpoint peer, out ReadOnlySpan<byte> data)
    {
        peer = default;
        data = default;

        if (State == TurnState.Idle || source != _server.Endpoint)
        {
            return false;
        }

        if (datagram.Length >= TurnFormat.ChannelHeaderLength && (datagram[0] & ChannelByteMask) == ChannelByte)
        {
            ReadChannelData(datagram, out peer, out data);

            return true;
        }

        if (!StunReader.IsMessage(datagram))
        {
            return true;
        }

        if (StunReader.Type(datagram) == TurnFormat.DataIndication)
        {
            ReadDataIndication(datagram, out peer, out data);

            return true;
        }

        HandleAnswer(datagram);

        return true;
    }

    /// <summary>Gives the allocation back (a refresh with lifetime 0), sent once; the client is idle after it.</summary>
    public void Close()
    {
        if (State == TurnState.Allocated && _integrity is not null)
        {
            NewTransaction(_allocation);
            var writer = new StunWriter(_send, TurnFormat.RefreshRequest, _allocation.TransactionId);
            writer.AddUInt32(TurnFormat.Lifetime, 0);
            Authenticate(ref writer);
            _transport.TrySend(_server.Endpoint, writer.Written);
        }

        _allocation.Active = false;
        State = TurnState.Idle;
    }

    private void ReadChannelData(ReadOnlySpan<byte> datagram, out DatagramEndpoint peer, out ReadOnlySpan<byte> data)
    {
        peer = default;
        data = default;
        ushort channel = BinaryPrimitives.ReadUInt16BigEndian(datagram);
        int length = BinaryPrimitives.ReadUInt16BigEndian(datagram[LengthOffset..]);

        if (TurnFormat.ChannelHeaderLength + length > datagram.Length)
        {
            return;
        }

        for (int index = 0; index < _peerCount; index++)
        {
            if (_peers[index].Channel == channel)
            {
                peer = _peers[index].Endpoint;
                data = datagram.Slice(TurnFormat.ChannelHeaderLength, length);

                return;
            }
        }
    }

    private static void ReadDataIndication(ReadOnlySpan<byte> message, out DatagramEndpoint peer, out ReadOnlySpan<byte> data)
    {
        peer = default;
        data = default;

        if (StunReader.TryFind(message, TurnFormat.XorPeerAddress, out var address, out _)
            && StunReader.TryReadXorMappedAddress(address, out var from)
            && StunReader.TryFind(message, TurnFormat.Data, out var carried, out _))
        {
            peer = from;
            data = carried;
        }
    }

    private void ServiceAllocation(long now)
    {
        if (_allocation.Active && now >= _allocation.DueAt)
        {
            if (_allocation.Attempts >= TurnTiming.MaxAttempts)
            {
                _allocation.Active = false;
                State = TurnState.Failed;

                return;
            }

            _allocation.Timeout += _allocation.Timeout;
            Transmit(_allocation, now);
            SendAllocation();
        }
        else if (State == TurnState.Allocated && !_allocation.Active && now >= _refreshAt)
        {
            _refreshing = true;
            Begin(_allocation, now);
            SendAllocation();
        }
    }

    private void ServicePeers(long now)
    {
        for (int index = 0; index < _peerCount; index++)
        {
            var peer = _peers[index];
            var request = peer.Request;

            if (peer.Refused)
            {
                continue;
            }

            if (!request.Active)
            {
                if (now >= peer.RebindAt)
                {
                    Begin(request, now);
                    SendChannelBind(peer);
                }

                continue;
            }

            if (now < request.DueAt)
            {
                continue;
            }

            if (request.Attempts >= TurnTiming.MaxAttempts)
            {
                // Unanswered: tried again at the next renewal.
                request.Active = false;
                peer.RebindAt = now + Ticks(TurnTiming.ChannelRefresh);

                continue;
            }

            request.Timeout += request.Timeout;
            Transmit(request, now);
            SendChannelBind(peer);
        }
    }

    private void HandleAnswer(ReadOnlySpan<byte> answer)
    {
        var id = StunReader.TransactionId(answer);

        if (_allocation.Active && id.SequenceEqual(_allocation.TransactionId))
        {
            _allocation.Active = false;
            AllocationAnswered(answer);

            return;
        }

        for (int index = 0; index < _peerCount; index++)
        {
            var peer = _peers[index];

            if (peer.Request.Active && id.SequenceEqual(peer.Request.TransactionId))
            {
                peer.Request.Active = false;
                BindingAnswered(peer, answer);

                return;
            }
        }
    }

    private void AllocationAnswered(ReadOnlySpan<byte> answer)
    {
        ushort type = StunReader.Type(answer);
        long now = _clock.GetTimestamp();

        if ((type == TurnFormat.AllocateSuccess || type == TurnFormat.RefreshSuccess) && Verified(answer))
        {
            if (type == TurnFormat.AllocateSuccess && !Allocated(answer))
            {
                State = TurnState.Failed;

                return;
            }

            _refreshAt = now + (Ticks(Lifetime(answer)) >> HalvingShift);

            return;
        }

        if (type != TurnFormat.AllocateError && type != TurnFormat.RefreshError)
        {
            return;
        }

        if (Challenged(answer))
        {
            Begin(_allocation, now);
            SendAllocation();

            return;
        }

        Refusal = StunReader.ErrorCode(answer);
        State = TurnState.Failed;
    }

    /// <summary>Reads the relayed and mapped addresses of a successful allocation; false without a relayed one.</summary>
    private bool Allocated(ReadOnlySpan<byte> answer)
    {
        if (!StunReader.TryFind(answer, TurnFormat.XorRelayedAddress, out var relayed, out _) || !StunReader.TryReadXorMappedAddress(relayed, out var relayedAt))
        {
            return false;
        }

        Relayed = relayedAt;

        if (StunReader.TryFind(answer, StunFormat.XorMappedAddress, out var mapped, out _) && StunReader.TryReadXorMappedAddress(mapped, out var mappedAt))
        {
            Mapped = mappedAt;
        }

        State = TurnState.Allocated;

        return true;
    }

    private void BindingAnswered(TurnPeer peer, ReadOnlySpan<byte> answer)
    {
        ushort type = StunReader.Type(answer);
        long now = _clock.GetTimestamp();

        if (type == TurnFormat.ChannelBindSuccess && Verified(answer))
        {
            peer.Bound = true;
            peer.RebindAt = now + Ticks(TurnTiming.ChannelRefresh);
        }
        else if (type == TurnFormat.ChannelBindError && Challenged(answer))
        {
            Begin(peer.Request, now);
            SendChannelBind(peer);
        }
        else if (type == TurnFormat.ChannelBindError)
        {
            peer.Refused = true;
        }
    }

    /// <summary>
    /// The server's challenge (401 before we authenticated, or 438 for a stale nonce): its realm and nonce are kept and
    /// the long-term key made, so the request can go again. False for any other refusal.
    /// </summary>
    private bool Challenged(ReadOnlySpan<byte> answer)
    {
        int code = StunReader.ErrorCode(answer);
        bool challenge = (code == TurnFormat.Unauthenticated && _integrity is null) || code == TurnFormat.StaleNonce;

        if (!challenge || !StunReader.TryFind(answer, TurnFormat.Nonce, out var nonce, out _) || nonce.Length > _nonce.Length)
        {
            return false;
        }

        nonce.CopyTo(_nonce);
        _nonceLength = nonce.Length;

        if (StunReader.TryFind(answer, TurnFormat.Realm, out var realm, out _) && realm.Length <= _realm.Length)
        {
            realm.CopyTo(_realm);
            _realmLength = realm.Length;
        }

        _integrity = new StunIntegrity(LongTermKey());

        return true;
    }

    /// <summary>MD5 of "username:realm:password" (RFC 8489 section 9.2.2).</summary>
    private byte[] LongTermKey()
    {
        string realm = Encoding.UTF8.GetString(_realm, 0, _realmLength);
        byte[] text = Encoding.UTF8.GetBytes(string.Concat(_server.Username, Separator.ToString(), realm, Separator.ToString(), _server.Password));
        var digest = new MD5Digest();
        byte[] key = new byte[digest.GetDigestSize()];
        digest.BlockUpdate(text, 0, text.Length);
        digest.DoFinal(key, 0);

        return key;
    }

    private bool Verified(ReadOnlySpan<byte> answer)
    {
        return _integrity is not null && StunReader.HasValidIntegrity(answer, _integrity);
    }

    private static TimeSpan Lifetime(ReadOnlySpan<byte> answer)
    {
        if (StunReader.TryFind(answer, TurnFormat.Lifetime, out var value, out _) && value.Length >= sizeof(uint))
        {
            return TimeSpan.FromSeconds(BinaryPrimitives.ReadUInt32BigEndian(value));
        }

        return TurnTiming.DefaultLifetime;
    }

    private void SendAllocation()
    {
        ushort type = TurnFormat.AllocateRequest;

        if (_refreshing)
        {
            type = TurnFormat.RefreshRequest;
        }

        var writer = new StunWriter(_send, type, _allocation.TransactionId);

        if (!_refreshing)
        {
            writer.AddUInt32(TurnFormat.RequestedTransport, (uint)TurnFormat.UdpProtocol << TurnFormat.RequestedTransportShift);
        }

        Authenticate(ref writer);
        _transport.TrySend(_server.Endpoint, writer.Written);
    }

    private void SendChannelBind(TurnPeer peer)
    {
        var writer = new StunWriter(_send, TurnFormat.ChannelBindRequest, peer.Request.TransactionId);
        writer.AddUInt32(TurnFormat.ChannelNumber, (uint)peer.Channel << TurnFormat.ChannelNumberShift);
        writer.AddXorAddress(TurnFormat.XorPeerAddress, peer.Endpoint);
        Authenticate(ref writer);
        _transport.TrySend(_server.Endpoint, writer.Written);
    }

    /// <summary>USERNAME, REALM, NONCE and MESSAGE-INTEGRITY once challenged, then FINGERPRINT.</summary>
    private void Authenticate(ref StunWriter writer)
    {
        if (_integrity is not null)
        {
            writer.Add(StunFormat.Username, _username);
            writer.Add(TurnFormat.Realm, _realm.AsSpan(0, _realmLength));
            writer.Add(TurnFormat.Nonce, _nonce.AsSpan(0, _nonceLength));
            writer.AddIntegrity(_integrity);
        }

        writer.AddFingerprint();
    }

    private TurnPeer? PeerAt(DatagramEndpoint endpoint)
    {
        for (int index = 0; index < _peerCount; index++)
        {
            if (_peers[index].Endpoint == endpoint)
            {
                return _peers[index];
            }
        }

        return null;
    }

    private void Begin(TurnRequest request, long now)
    {
        NewTransaction(request);
        request.Active = true;
        request.Attempts = 0;
        request.Timeout = TurnTiming.FirstTimeout;
        Transmit(request, now);
    }

    private void Transmit(TurnRequest request, long now)
    {
        request.Attempts++;
        request.DueAt = now + Ticks(request.Timeout);
    }

    private void NewTransaction(TurnRequest request)
    {
        var id = request.TransactionId.AsSpan();

        for (int offset = 0; offset < id.Length; offset += sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(id[offset..], _random.NextUInt32());
        }
    }

    private long Ticks(TimeSpan span)
    {
        return span.Ticks * _clock.TimestampFrequency / TimeSpan.TicksPerSecond;
    }
}
