// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>
/// What an MP4 file's video holds, as Media Foundation's source reader reads it back: its samples (frames) and how many
/// are key frames, its size, its length, its codec and the file's bytes.
/// </summary>
public sealed record Mp4Facts(int Frames, int KeyFrames, int Width, int Height, TimeSpan Duration, string Codec, long Bytes)
{
    private const double BitsPerByte = 8;
    private const double BitsPerMegabit = 1_000_000;

    /// <summary>The file's mean bitrate in Mbit/s over its length; 0 for an empty file.</summary>
    public double MegabitsPerSecond
    {
        get
        {
            if (Duration <= TimeSpan.Zero)
            {
                return 0;
            }

            return Bytes * BitsPerByte / BitsPerMegabit / Duration.TotalSeconds;
        }
    }
}
