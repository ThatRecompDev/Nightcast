// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The value of INIT and INIT ACK (RFC 9260 sections 3.3.2 and 3.3.3): the fixed fields, then FORWARD-TSN-Supported
/// and Supported Extensions naming FORWARD TSN (RFC 3758), and in an INIT ACK the state cookie.
/// </summary>
internal static class SctpInitChunk
{
    private const int WindowOffset = 4;
    private const int OutboundOffset = 8;
    private const int InboundOffset = 10;
    private const int InitialTsnOffset = 12;

    /// <summary>The value's length with a cookie of <paramref name="cookieLength"/> bytes (none for an INIT).</summary>
    public static int Length(int cookieLength)
    {
        int length = SctpFormat.InitFixedLength + SctpPacketBuilder.ParameterLength(0) + SctpPacketBuilder.ParameterLength(Extensions.Length);

        if (cookieLength > 0)
        {
            length += SctpPacketBuilder.ParameterLength(cookieLength);
        }

        return length;
    }

    public static void Write(Span<byte> value, SctpInitParameters init, ReadOnlySpan<byte> cookie)
    {
        BinaryPrimitives.WriteUInt32BigEndian(value, init.InitiateTag);
        BinaryPrimitives.WriteUInt32BigEndian(value[WindowOffset..], init.Window);
        BinaryPrimitives.WriteUInt16BigEndian(value[OutboundOffset..], init.OutboundStreams);
        BinaryPrimitives.WriteUInt16BigEndian(value[InboundOffset..], init.InboundStreams);
        BinaryPrimitives.WriteUInt32BigEndian(value[InitialTsnOffset..], init.InitialTsn);

        int at = SctpFormat.InitFixedLength;
        at += SctpPacketBuilder.WriteParameter(value[at..], SctpFormat.ForwardTsnSupported, ReadOnlySpan<byte>.Empty);
        at += SctpPacketBuilder.WriteParameter(value[at..], SctpFormat.SupportedExtensions, Extensions);

        if (!cookie.IsEmpty)
        {
            SctpPacketBuilder.WriteParameter(value[at..], SctpFormat.StateCookie, cookie);
        }
    }

    /// <summary>Reads the fields and parameters; false when the value is short or its tag is zero, which RFC 9260 forbids.</summary>
    public static bool TryRead(ReadOnlySpan<byte> value, out SctpInitParameters init, out ReadOnlySpan<byte> cookie)
    {
        init = default;
        cookie = default;

        if (value.Length < SctpFormat.InitFixedLength || BinaryPrimitives.ReadUInt32BigEndian(value) == 0)
        {
            return false;
        }

        bool forwardTsn = false;
        int offset = SctpFormat.InitFixedLength;

        while (SctpPacket.TryReadParameter(value, ref offset, out ushort type, out var parameter))
        {
            if (type == SctpFormat.StateCookie)
            {
                cookie = parameter;
            }
            else if (type == SctpFormat.ForwardTsnSupported || (type == SctpFormat.SupportedExtensions && parameter.Contains(SctpFormat.ForwardTsn)))
            {
                forwardTsn = true;
            }
        }

        init = new SctpInitParameters(
            BinaryPrimitives.ReadUInt32BigEndian(value),
            BinaryPrimitives.ReadUInt32BigEndian(value[WindowOffset..]),
            BinaryPrimitives.ReadUInt16BigEndian(value[OutboundOffset..]),
            BinaryPrimitives.ReadUInt16BigEndian(value[InboundOffset..]),
            BinaryPrimitives.ReadUInt32BigEndian(value[InitialTsnOffset..]),
            forwardTsn);

        return true;
    }

    /// <summary>The chunk types beyond RFC 9260 this side understands.</summary>
    private static ReadOnlySpan<byte> Extensions => [SctpFormat.ForwardTsn];
}
