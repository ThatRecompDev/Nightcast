// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows;

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// An H.264 track: the picture's size and the stream's sequence and picture parameter sets (each a NAL unit, header
/// byte first, no start code), which make its avcC. Its clock is RTP's 90 kHz (<see cref="Timescale"/>).
/// </summary>
public sealed record Mp4VideoTrack(int Width, int Height, ReadOnlyMemory<byte> SequenceParameterSet, ReadOnlyMemory<byte> PictureParameterSet)
{
    /// <summary>The track's time units a second: H.264's RTP clock, so its timestamps go in as they are.</summary>
    public const int Timescale = 90000;

    /// <summary>The bytes of an SPS up to its level_idc (its header, profile_idc, the constraint flags, level_idc).</summary>
    public const int SequenceHeaderLength = 4;

    /// <summary>
    /// The track a stream of <paramref name="width"/> x <paramref name="height"/> starting at <paramref name="keyUnit"/>
    /// (Annex-B) makes, from the parameter sets it carries; null when it carries none.
    /// </summary>
    public static Mp4VideoTrack? FromKeyFrame(ReadOnlySpan<byte> keyUnit, int width, int height)
    {
        if (!AnnexBUnits.TryFind(keyUnit, AnnexBUnits.SequenceParameterSetType, out var sequence)
            || !AnnexBUnits.TryFind(keyUnit, AnnexBUnits.PictureParameterSetType, out var picture)
            || sequence.Length < SequenceHeaderLength)
        {
            return null;
        }

        return new Mp4VideoTrack(width, height, sequence.ToArray(), picture.ToArray());
    }
}
