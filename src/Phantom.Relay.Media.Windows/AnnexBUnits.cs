// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// The NAL units of an H.264 access unit in Annex-B form (ITU-T H.264 Annex B: each unit after a start code of
/// 00 00 01 or 00 00 00 01). The decoder reads the stream's colour from a sequence parameter set the unit carries and
/// knows a key frame by its IDR slice. Scanning allocates nothing.
/// </summary>
public static class AnnexBUnits
{
    /// <summary>The NAL unit type of an IDR picture's slice (H.264 table 7-1).</summary>
    public const int IdrSliceType = 5;

    /// <summary>The NAL unit type of a sequence parameter set (H.264 table 7-1).</summary>
    public const int SequenceParameterSetType = 7;

    /// <summary>The bits of a NAL unit's first byte that hold its type.</summary>
    public const int NalTypeMask = 0x1F;

    // A start code is two zero bytes and a one; a fourth, leading zero belongs to the unit before as trailing zeros.
    private const int ShortStartCodeLength = 3;
    private const int StartCodeZeros = 2;

    /// <summary>The type of a NAL unit (its first byte without the header bits); -1 for an empty unit.</summary>
    [HotPath]
    public static int TypeOf(ReadOnlySpan<byte> unit)
    {
        if (unit.IsEmpty)
        {
            return -1;
        }

        return unit[0] & NalTypeMask;
    }

    /// <summary>
    /// The next NAL unit at or after <paramref name="position"/> (header byte first, start code and trailing zeros left
    /// out), moving <paramref name="position"/> past it; false when no start code follows.
    /// </summary>
    [HotPath]
    public static bool TryNext(ReadOnlySpan<byte> accessUnit, scoped ref int position, out ReadOnlySpan<byte> unit)
    {
        unit = ReadOnlySpan<byte>.Empty;
        var start = FindStartCode(accessUnit, position);

        if (start < 0)
        {
            position = accessUnit.Length;

            return false;
        }

        var begin = start + ShortStartCodeLength;
        var next = FindStartCode(accessUnit, begin);
        var end = next;

        if (next < 0)
        {
            next = accessUnit.Length;
            end = accessUnit.Length;
        }

        // The zero bytes before the next start code (its fourth byte, or trailing_zero_8bits) are not the unit's.
        while (end > begin && accessUnit[end - 1] == 0)
        {
            end--;
        }

        unit = accessUnit[begin..end];
        position = next;

        return true;
    }

    /// <summary>The first NAL unit of <paramref name="type"/> in the access unit; false when it has none.</summary>
    [HotPath]
    public static bool TryFind(ReadOnlySpan<byte> accessUnit, int type, out ReadOnlySpan<byte> found)
    {
        var position = 0;

        while (TryNext(accessUnit, ref position, out var unit))
        {
            if (TypeOf(unit) == type)
            {
                found = unit;

                return true;
            }
        }

        found = ReadOnlySpan<byte>.Empty;

        return false;
    }

    /// <summary>Whether the access unit holds an IDR slice: a key frame, from which decoding can start.</summary>
    [HotPath]
    public static bool IsKeyFrame(ReadOnlySpan<byte> accessUnit)
    {
        return TryFind(accessUnit, IdrSliceType, out _);
    }

    // The offset of the next 00 00 01 at or after from; -1 when there is none.
    [HotPath]
    private static int FindStartCode(ReadOnlySpan<byte> data, int from)
    {
        var zeros = 0;

        for (var index = from; index < data.Length; index++)
        {
            var value = data[index];

            if (value == 1 && zeros >= StartCodeZeros)
            {
                return index - StartCodeZeros;
            }

            if (value == 0)
            {
                zeros++;
            }
            else
            {
                zeros = 0;
            }
        }

        return -1;
    }
}
