// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

/// <summary>
/// An H.264 elementary stream (Annex-B) split into access units at its access unit delimiters (NAL type 9, which
/// x264 writes with aud=1), each with whether it holds an IDR slice. For the live check's stream made by ffmpeg.
/// </summary>
public static class AnnexBAccessUnits
{
    private const int Delimiter = 9;
    private const int Idr = 5;

    public static List<(byte[] Unit, bool Key)> Split(byte[] stream)
    {
        var starts = new List<int>();

        for (var at = 0; at + 4 < stream.Length; at++)
        {
            if (stream[at] == 0 && stream[at + 1] == 0 && stream[at + 2] == 0 && stream[at + 3] == 1 && (stream[at + 4] & 0x1F) == Delimiter)
            {
                starts.Add(at);
            }
        }

        starts.Add(stream.Length);

        return starts.Zip(starts.Skip(1), (start, end) => stream[start..end]).Select(unit => (unit, HasIdr(unit))).ToList();
    }

    private static bool HasIdr(byte[] unit)
    {
        for (var at = 0; at + 3 < unit.Length; at++)
        {
            if (unit[at] == 0 && unit[at + 1] == 0 && unit[at + 2] == 1 && (unit[at + 3] & 0x1F) == Idr)
            {
                return true;
            }
        }

        return false;
    }
}
