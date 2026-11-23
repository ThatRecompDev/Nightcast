// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// The video's trak (ISO/IEC 14496-12 and -15): its header at the picture's size, a 90 kHz media header, the video
/// handler, vmhd, and one avc1 sample entry with the stream's avcC.
/// </summary>
internal static class VideoTrack
{
    public const string HandlerName = "VideoHandler";

    // VisualSampleEntry: 72 dpi (16.16), one frame a sample, no compressor name, 24-bit colour, pre_defined -1.
    private const uint Resolution = 0x00480000;
    private const int FramesPerSample = 1;
    private const int CompressorNameBytes = 32;
    private const int Depth = 0x0018;
    private const int NoColourTable = 0xFFFF;
    private const int ReservedEntryBytes = 6;
    private const int DataReferenceIndex = 1;
    private const int PreDefinedBytes = 16;

    // vmhd's flags must be 1; its graphics mode and colour are zero (copy).
    private const uint VideoHeaderFlags = 1;
    private const int VideoHeaderBytes = 8;

    public static void Write(BoxBuffer buffer, Mp4VideoTrack track)
    {
        var trak = buffer.Begin(Mp4BoxType.Track);
        InitSegment.WriteTrackHeader(buffer, InitSegment.VideoTrackId, 0, track.Width, track.Height);
        var media = buffer.Begin(Mp4BoxType.Media);
        InitSegment.WriteMediaHeaders(buffer, Mp4VideoTrack.Timescale, Mp4BoxType.HandlerVideo, HandlerName);

        var information = buffer.Begin(Mp4BoxType.MediaInformation);
        var header = buffer.BeginFull(Mp4BoxType.VideoMediaHeader, 0, VideoHeaderFlags);
        buffer.Zeros(VideoHeaderBytes);
        buffer.End(header);
        InitSegment.WriteDataAndSamples(buffer, entries => WriteEntry(entries, track));

        buffer.End(information);
        buffer.End(media);
        buffer.End(trak);
    }

    private static void WriteEntry(BoxBuffer buffer, Mp4VideoTrack track)
    {
        var entry = buffer.Begin(Mp4BoxType.Avc1);
        buffer.Zeros(ReservedEntryBytes);
        buffer.U16(DataReferenceIndex);
        buffer.Zeros(PreDefinedBytes);
        buffer.U16(track.Width);
        buffer.U16(track.Height);

        buffer.U32(Resolution);
        buffer.U32(Resolution);
        buffer.U32(0);
        buffer.U16(FramesPerSample);
        buffer.Zeros(CompressorNameBytes);
        buffer.U16(Depth);

        buffer.U16(NoColourTable);
        AvcConfiguration.Write(buffer, track);
        buffer.End(entry);
    }
}
