// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>The fixed values of TURN (RFC 8656) over UDP: its methods, attributes, error codes and ChannelData.</summary>
public static class TurnFormat
{
    public const ushort AllocateRequest = 0x0003;

    public const ushort AllocateSuccess = 0x0103;

    public const ushort AllocateError = 0x0113;

    public const ushort RefreshRequest = 0x0004;

    public const ushort RefreshSuccess = 0x0104;

    public const ushort RefreshError = 0x0114;

    public const ushort ChannelBindRequest = 0x0009;

    public const ushort ChannelBindSuccess = 0x0109;

    public const ushort ChannelBindError = 0x0119;

    public const ushort DataIndication = 0x0017;

    public const ushort ChannelNumber = 0x000C;

    public const ushort Lifetime = 0x000D;

    public const ushort XorPeerAddress = 0x0012;

    public const ushort Data = 0x0013;

    public const ushort Realm = 0x0014;

    public const ushort Nonce = 0x0015;

    public const ushort XorRelayedAddress = 0x0016;

    public const ushort RequestedTransport = 0x0019;

    /// <summary>REQUESTED-TRANSPORT's protocol number for UDP, followed by three reserved bytes.</summary>
    public const byte UdpProtocol = 17;

    /// <summary>The protocol number is REQUESTED-TRANSPORT's first byte of four.</summary>
    public const int RequestedTransportShift = 24;

    /// <summary>CHANNEL-NUMBER: the channel in the first two bytes, two reserved bytes after it.</summary>
    public const int ChannelNumberShift = 16;

    public const int Unauthenticated = 401;

    public const int StaleNonce = 438;

    /// <summary>The first and last channel numbers a client may bind (RFC 8656 section 12).</summary>
    public const ushort FirstChannel = 0x4000;

    public const ushort LastChannel = 0x4FFF;

    /// <summary>ChannelData: the channel number and the data's length, two bytes each, then the data.</summary>
    public const int ChannelHeaderLength = 4;
}
