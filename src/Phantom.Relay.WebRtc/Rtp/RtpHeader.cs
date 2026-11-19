// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// One RTP packet's header (RFC 3550 section 5.1), read into place: an owner keeps one instance and reads every packet
/// into it, so parsing allocates nothing. Offsets point into the packet that was read; the CSRCs, the header extension
/// and the payload are sliced from it on demand.
/// </summary>
public sealed class RtpHeader
{
    public bool HasPadding { get; private set; }

    public bool HasExtension { get; private set; }

    public int CsrcCount { get; private set; }

    public bool Marker { get; private set; }

    public int PayloadType { get; private set; }

    public ushort SequenceNumber { get; private set; }

    public uint Timestamp { get; private set; }

    public uint Ssrc { get; private set; }

    /// <summary>The header extension's profile (0xBEDE for one-byte elements, 0x100x for two-byte ones), 0 without one.</summary>
    public ushort ExtensionProfile { get; private set; }

    /// <summary>Where the extension's elements start, after its own four-octet header.</summary>
    public int ExtensionOffset { get; private set; }

    public int ExtensionLength { get; private set; }

    /// <summary>Where the payload starts: the length of the whole header.</summary>
    public int PayloadOffset { get; private set; }

    public int PayloadLength { get; private set; }

    /// <summary>The padding octets at the end, the last of which counts them.</summary>
    public int PaddingLength { get; private set; }

    /// <summary>Reads <paramref name="packet"/>'s header; false when it is not a well-formed RTP version 2 packet.</summary>
    [HotPath]
    public bool TryRead(ReadOnlySpan<byte> packet)
    {
        if (!RtpFormat.TryGetHeaderLength(packet, out int headerLength))
        {
            return false;
        }

        int padding = 0;

        if ((packet[0] & RtpFormat.PaddingBit) != 0)
        {
            padding = packet[^1];

            if (padding == 0 || headerLength + padding > packet.Length)
            {
                return false;
            }
        }

        ReadFixed(packet);
        ReadExtension(packet);
        PayloadOffset = headerLength;
        PaddingLength = padding;
        PayloadLength = packet.Length - headerLength - padding;

        return true;
    }

    /// <summary>The <paramref name="index"/>th contributing source.</summary>
    public uint Csrc(ReadOnlySpan<byte> packet, int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)CsrcCount, nameof(index));

        return BinaryPrimitives.ReadUInt32BigEndian(packet[(RtpFormat.FixedHeaderLength + (index * RtpFormat.CsrcLength))..]);
    }

    /// <summary>The payload of the packet this header was read from.</summary>
    public ReadOnlySpan<byte> Payload(ReadOnlySpan<byte> packet)
    {
        return packet.Slice(PayloadOffset, PayloadLength);
    }

    /// <summary>The header extension's elements (empty without one).</summary>
    public ReadOnlySpan<byte> Extension(ReadOnlySpan<byte> packet)
    {
        return packet.Slice(ExtensionOffset, ExtensionLength);
    }

    private void ReadFixed(ReadOnlySpan<byte> packet)
    {
        HasPadding = (packet[0] & RtpFormat.PaddingBit) != 0;
        HasExtension = (packet[0] & RtpFormat.ExtensionBit) != 0;
        CsrcCount = packet[0] & RtpFormat.CsrcCountMask;
        Marker = (packet[1] & RtpFormat.MarkerBit) != 0;
        PayloadType = packet[1] & RtpFormat.PayloadTypeMask;
        SequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packet[RtpFormat.SequenceOffset..]);

        Timestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[RtpFormat.TimestampOffset..]);
        Ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet[RtpFormat.SsrcOffset..]);
    }

    private void ReadExtension(ReadOnlySpan<byte> packet)
    {
        ExtensionProfile = 0;
        ExtensionOffset = 0;
        ExtensionLength = 0;

        if (!HasExtension)
        {
            return;
        }

        int at = RtpFormat.FixedHeaderLength + (CsrcCount * RtpFormat.CsrcLength);
        ExtensionProfile = BinaryPrimitives.ReadUInt16BigEndian(packet[at..]);
        ExtensionOffset = at + RtpFormat.ExtensionHeaderLength;
        ExtensionLength = BinaryPrimitives.ReadUInt16BigEndian(packet[(at + sizeof(ushort))..]) * RtpFormat.WordLength;
    }
}
