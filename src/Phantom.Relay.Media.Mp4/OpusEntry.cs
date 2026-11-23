// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// The Opus sample entry and its dOps box (Encapsulation of Opus in ISO Base Media File Format, 4.3.1 and 4.3.2): an
/// AudioSampleEntry of the decoded channels, 16-bit, at 48 kHz, then dOps (version 0, the channels, the pre-skip, the
/// input's rate, no gain, channel mapping family 0). Unlike Ogg's OpusHead, dOps is big-endian.
/// </summary>
internal static class OpusEntry
{
    /// <summary>dOps's Version.</summary>
    public const int Version = 0;

    /// <summary>ChannelMappingFamily 0: mono or stereo, no mapping table.</summary>
    public const int MappingFamily = 0;

    /// <summary>The sample entry's samplesize: 16, as the specification asks.</summary>
    public const int SampleSize = 16;

    // A sample entry's six reserved bytes, then its data_reference_index (the one 'url ' entry: this file).
    private const int ReservedEntryBytes = 6;
    private const int DataReferenceIndex = 1;

    // AudioSampleEntry's reserved and pre-defined fields around the channels and the size.
    private const int ReservedAudioBytes = 8;
    private const int SampleRateShift = 16;

    /// <summary>Writes the sample entry of <paramref name="track"/>, its dOps inside.</summary>
    public static void Write(BoxBuffer buffer, Mp4AudioTrack track)
    {
        var entry = buffer.Begin(Mp4BoxType.Opus);
        buffer.Zeros(ReservedEntryBytes);
        buffer.U16(DataReferenceIndex);
        buffer.Zeros(ReservedAudioBytes);
        buffer.U16(track.Channels);
        buffer.U16(SampleSize);

        buffer.U16(0);
        buffer.U16(0);
        buffer.U32((uint)Mp4AudioTrack.Timescale << SampleRateShift);
        WriteSpecific(buffer, track);
        buffer.End(entry);
    }

    private static void WriteSpecific(BoxBuffer buffer, Mp4AudioTrack track)
    {
        var box = buffer.Begin(Mp4BoxType.OpusSpecific);
        buffer.U8(Version);
        buffer.U8(track.Channels);
        buffer.U16(track.PreSkip);
        buffer.U32(Mp4AudioTrack.Timescale);
        buffer.U16(0);

        buffer.U8(MappingFamily);
        buffer.End(box);
    }
}
