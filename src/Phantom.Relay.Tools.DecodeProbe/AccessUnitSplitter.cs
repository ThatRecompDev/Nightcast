// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows;

namespace Phantom.Relay.Tools.DecodeProbe;

/// <summary>
/// Splits an Annex-B H.264 elementary stream into access units at its access unit delimiters (NAL type 9), the way
/// the stream hands the decoder one whole access unit at a time. The sample must carry delimiters (FFmpeg writes them
/// with <c>-bsf:v h264_metadata=aud=insert</c>).
/// </summary>
internal static class AccessUnitSplitter
{
    /// <summary>The NAL unit type of an access unit delimiter (H.264 table 7-1).</summary>
    public const int DelimiterType = 9;

    // 00 00 01 before a unit's header; a fourth, leading zero stays with the unit before as a trailing zero.
    private const int StartCodeLength = 3;

    /// <summary>The access units of <paramref name="stream"/>, each from its delimiter's start code to the next one's.</summary>
    public static List<Range> Split(ReadOnlySpan<byte> stream)
    {
        var starts = new List<int>();
        var position = 0;

        while (AnnexBUnits.TryNext(stream, ref position, out var unit))
        {
            if (AnnexBUnits.TypeOf(unit) == DelimiterType && stream.Overlaps(unit, out var header))
            {
                starts.Add(header - StartCodeLength);
            }
        }

        var units = new List<Range>(starts.Count);

        for (var index = 0; index < starts.Count; index++)
        {
            var end = stream.Length;

            if (index + 1 < starts.Count)
            {
                end = starts[index + 1];
            }

            units.Add(new Range(starts[index], end));
        }

        return units;
    }
}
