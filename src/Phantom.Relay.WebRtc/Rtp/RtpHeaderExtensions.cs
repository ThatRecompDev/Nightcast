// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// Finds one element of an RTP header extension by its local id (RFC 8285): one-byte elements (profile 0xBEDE, ids 1
/// to 14, a 4-bit length of one less than the data) and two-byte elements (profile 0x100x, ids 1 to 255, an 8-bit
/// length). Padding octets (0) between elements are skipped; id 15 ends a one-byte list.
/// </summary>
public static class RtpHeaderExtensions
{
    private const int IdShift = 4;
    private const int LengthMask = 0x0F;

    /// <summary>The one-byte id that stops the parsing of the rest of the extension.</summary>
    private const int OneByteStop = 15;

    private const int TwoByteElementHeader = 2;

    /// <summary>The data of element <paramref name="id"/> in the header extension of <paramref name="packet"/>.</summary>
    [HotPath]
    public static bool TryFind(ReadOnlySpan<byte> packet, RtpHeader header, int id, out ReadOnlySpan<byte> value)
    {
        ArgumentNullException.ThrowIfNull(header);

        value = default;

        if (!header.HasExtension || id <= 0)
        {
            return false;
        }

        var elements = header.Extension(packet);

        if (header.ExtensionProfile == RtpFormat.OneByteProfile)
        {
            return TryFindOneByte(elements, id, out value);
        }

        if ((header.ExtensionProfile & RtpFormat.TwoByteProfileMask) == RtpFormat.TwoByteProfile)
        {
            return TryFindTwoByte(elements, id, out value);
        }

        return false;
    }

    private static bool TryFindOneByte(ReadOnlySpan<byte> elements, int id, out ReadOnlySpan<byte> value)
    {
        value = default;
        int at = 0;

        while (at < elements.Length)
        {
            int found = elements[at] >> IdShift;
            int length = (elements[at] & LengthMask) + 1;

            if (elements[at] == 0)
            {
                at++;
                continue;
            }

            if (found == OneByteStop || at + 1 + length > elements.Length)
            {
                return false;
            }

            if (found == id)
            {
                value = elements.Slice(at + 1, length);

                return true;
            }

            at += 1 + length;
        }

        return false;
    }

    private static bool TryFindTwoByte(ReadOnlySpan<byte> elements, int id, out ReadOnlySpan<byte> value)
    {
        value = default;
        int at = 0;

        while (at < elements.Length)
        {
            if (elements[at] == 0)
            {
                at++;
                continue;
            }

            if (at + TwoByteElementHeader > elements.Length || at + TwoByteElementHeader + elements[at + 1] > elements.Length)
            {
                return false;
            }

            int length = elements[at + 1];

            if (elements[at] == id)
            {
                value = elements.Slice(at + TwoByteElementHeader, length);

                return true;
            }

            at += TwoByteElementHeader + length;
        }

        return false;
    }
}
