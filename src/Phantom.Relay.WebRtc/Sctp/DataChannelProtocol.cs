// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Text;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The WebRTC data channel wire format: the payload protocol identifiers of RFC 8831 section 8 and the Data Channel
/// Establishment Protocol of RFC 8832, whose DATA_CHANNEL_OPEN names a channel's type, priority, reliability, label
/// and protocol, and whose DATA_CHANNEL_ACK confirms it.
/// </summary>
public static class DataChannelProtocol
{
    /// <summary>DCEP messages.</summary>
    public const uint ControlPayload = 50;

    /// <summary>A UTF-8 string message.</summary>
    public const uint StringPayload = 51;

    public const uint BinaryPayload = 53;

    /// <summary>An empty string message, sent as one zero byte since SCTP carries no empty message.</summary>
    public const uint EmptyStringPayload = 56;

    /// <summary>An empty binary message, sent as one zero byte.</summary>
    public const uint EmptyBinaryPayload = 57;

    public const byte OpenMessage = 0x03;
    public const byte AckMessage = 0x02;

    /// <summary>Channel type bit: messages are delivered as soon as they are whole.</summary>
    public const byte UnorderedBit = 0x80;

    public const byte ReliableType = 0x00;

    /// <summary>Given up after the reliability parameter's number of retransmissions.</summary>
    public const byte RetransmitLimitedType = 0x01;

    /// <summary>Given up after the reliability parameter's milliseconds.</summary>
    public const byte TimeLimitedType = 0x02;

    /// <summary>RFC 8831's "normal" priority, what browsers send by default.</summary>
    public const ushort NormalPriority = 256;

    /// <summary>Message type, channel type, priority, reliability parameter, label length and protocol length.</summary>
    public const int OpenFixedLength = 12;

    private const int ChannelTypeOffset = 1;
    private const int PriorityOffset = 2;
    private const int ReliabilityOffset = 4;
    private const int LabelLengthOffset = 8;
    private const int ProtocolLengthOffset = 10;

    /// <summary>The bytes of a DATA_CHANNEL_OPEN for these options.</summary>
    public static byte[] Open(DataChannelOptions options)
    {
        var label = Encoding.UTF8.GetBytes(options.Label);
        var protocol = Encoding.UTF8.GetBytes(options.Protocol);
        var message = new byte[OpenFixedLength + label.Length + protocol.Length];
        message[0] = OpenMessage;
        message[ChannelTypeOffset] = ChannelType(options);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(PriorityOffset), options.Priority);

        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(ReliabilityOffset), ReliabilityParameter(options));
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(LabelLengthOffset), (ushort)label.Length);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(ProtocolLengthOffset), (ushort)protocol.Length);
        label.CopyTo(message, OpenFixedLength);
        protocol.CopyTo(message, OpenFixedLength + label.Length);

        return message;
    }

    /// <summary>Reads a DATA_CHANNEL_OPEN; false when it is short, of another type or its lengths do not add up.</summary>
    public static bool TryReadOpen(ReadOnlySpan<byte> message, out DataChannelOptions options)
    {
        options = DataChannelOptions.Reliable(string.Empty, string.Empty);

        if (message.Length < OpenFixedLength || message[0] != OpenMessage)
        {
            return false;
        }

        int labelLength = BinaryPrimitives.ReadUInt16BigEndian(message[LabelLengthOffset..]);
        int protocolLength = BinaryPrimitives.ReadUInt16BigEndian(message[ProtocolLengthOffset..]);

        if (OpenFixedLength + labelLength + protocolLength > message.Length)
        {
            return false;
        }

        byte type = message[ChannelTypeOffset];
        options = new DataChannelOptions(
            Encoding.UTF8.GetString(message.Slice(OpenFixedLength, labelLength)),
            Encoding.UTF8.GetString(message.Slice(OpenFixedLength + labelLength, protocolLength)),
            (type & UnorderedBit) == 0,
            ReliabilityOf(type),
            BinaryPrimitives.ReadUInt32BigEndian(message[ReliabilityOffset..]),
            BinaryPrimitives.ReadUInt16BigEndian(message[PriorityOffset..]));

        return true;
    }

    public static byte ChannelType(DataChannelOptions options)
    {
        byte type = options.Reliability switch
        {
            SctpReliability.MaxRetransmits => RetransmitLimitedType,
            SctpReliability.Lifetime => TimeLimitedType,
            _ => ReliableType,
        };

        if (!options.Ordered)
        {
            type |= UnorderedBit;
        }

        return type;
    }

    private static SctpReliability ReliabilityOf(byte channelType)
    {
        return (channelType & ~UnorderedBit) switch
        {
            RetransmitLimitedType => SctpReliability.MaxRetransmits,
            TimeLimitedType => SctpReliability.Lifetime,
            _ => SctpReliability.Reliable,
        };
    }

    /// <summary>RFC 8832: the parameter is ignored for a reliable channel and sent as zero.</summary>
    private static uint ReliabilityParameter(DataChannelOptions options)
    {
        if (options.Reliability == SctpReliability.Reliable)
        {
            return 0;
        }

        return options.ReliabilityLimit;
    }
}
