// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows;

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// An access unit from Annex-B form (each NAL unit after a start code) to an MP4 sample's (each NAL unit after its
/// length, <see cref="AvcConfiguration.NalLengthSize"/> bytes big-endian, ISO/IEC 14496-15 5.3.2): every NAL unit is
/// kept, in-band parameter sets and delimiters too, and each one's start code and trailing zero bytes left out.
/// </summary>
internal static class LengthPrefixedUnits
{
    /// <summary>Writes <paramref name="accessUnit"/>'s NAL units into <paramref name="target"/>; the bytes written (0: it had none).</summary>
    public static int Write(ReadOnlySpan<byte> accessUnit, BoxBuffer target)
    {
        var position = 0;
        var written = 0;

        while (AnnexBUnits.TryNext(accessUnit, ref position, out var unit))
        {
            if (unit.IsEmpty)
            {
                continue;
            }

            target.U32((uint)unit.Length);
            target.Bytes(unit);
            written += AvcConfiguration.NalLengthSize + unit.Length;
        }

        return written;
    }
}
