// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// The avcC box (ISO/IEC 14496-15 5.3.3.1, AVCDecoderConfigurationRecord): version 1, the SPS's profile, constraint
/// flags and level, samples with 4-byte lengths before each NAL unit, then the one SPS and the one PPS, each after its
/// 16-bit length. The record's extension for the High profiles (chroma format and bit depths) is left out, as many
/// muxers leave it; decoders read those from the SPS.
/// </summary>
internal static class AvcConfiguration
{
    /// <summary>configurationVersion.</summary>
    public const int Version = 1;

    /// <summary>The bytes before each NAL unit of a sample: its length, 4 bytes big-endian.</summary>
    public const int NalLengthSize = 4;

    // The SPS's profile_idc, constraint flags and level_idc, after its header byte (H.264 7.3.2.1.1).
    private const int ProfileOffset = 1;
    private const int CompatibilityOffset = 2;
    private const int LevelOffset = 3;

    // Six reserved bits set, then lengthSizeMinusOne; three reserved bits set, then numOfSequenceParameterSets.
    private const int LengthSizeReserved = 0xFC;
    private const int SequenceCountReserved = 0xE0;
    private const int OneSet = 1;

    /// <summary>Writes the avcC of <paramref name="track"/> into <paramref name="buffer"/>.</summary>
    public static void Write(BoxBuffer buffer, Mp4VideoTrack track)
    {
        var sequence = track.SequenceParameterSet.Span;
        var picture = track.PictureParameterSet.Span;
        var box = buffer.Begin(Mp4BoxType.AvcConfiguration);
        buffer.U8(Version);
        buffer.U8(sequence[ProfileOffset]);
        buffer.U8(sequence[CompatibilityOffset]);

        buffer.U8(sequence[LevelOffset]);
        buffer.U8(LengthSizeReserved | (NalLengthSize - 1));
        buffer.U8(SequenceCountReserved | OneSet);
        buffer.U16(sequence.Length);
        buffer.Bytes(sequence);

        buffer.U8(OneSet);
        buffer.U16(picture.Length);
        buffer.Bytes(picture);
        buffer.End(box);
    }
}
