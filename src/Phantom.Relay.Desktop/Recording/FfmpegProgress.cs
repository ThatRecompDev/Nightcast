// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// ffmpeg's progress lines (<c>-progress pipe:1</c>: key=value, a block a half second): how far into the output it is
/// (<c>out_time_us</c>, microseconds) as a percent of the whole.
/// </summary>
internal static class FfmpegProgress
{
    private const string OutTime = "out_time_us=";
    private const double Whole = 100;

    /// <summary>The output time a progress line states; false for any other line (or "N/A", before the first frame).</summary>
    public static bool TryRead(string? line, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (line is null || !line.StartsWith(OutTime, StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(line.AsSpan(OutTime.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) || microseconds < 0)
        {
            return false;
        }

        time = TimeSpan.FromMicroseconds(microseconds);

        return true;
    }

    /// <summary><paramref name="done"/> of <paramref name="total"/> in whole percent, 0 to 100.</summary>
    public static int Percent(TimeSpan done, TimeSpan total)
    {
        if (total <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Clamp(done / total * Whole, 0, Whole);
    }
}
