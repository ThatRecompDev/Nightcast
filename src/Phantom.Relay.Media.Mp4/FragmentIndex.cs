// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// The fragments' random access index at the end of a finished file (ISO/IEC 14496-12 8.8.9 to 8.8.11): mfra with a
/// tfra for each track (version 1: 64-bit times and offsets; traf and trun numbers in a byte, sample numbers in four),
/// then mfro with mfra's size, so a player finds the index from the file's end and seeks without reading every moof.
/// A file cut short has none; players then read the fragments in order.
/// </summary>
internal sealed class FragmentIndex
{
    private const int VersionLong = 1;

    // length_size_of_traf_num 0 and length_size_of_trun_num 0 (one byte each), length_size_of_sample_num 3 (four bytes).
    private const uint FieldSizes = 0x3;
    private const int FirstRun = 1;

    private readonly List<RandomAccessPoint> _video = [];
    private readonly List<RandomAccessPoint> _audio = [];

    /// <summary>A fragment's first key frame.</summary>
    public void AddVideo(in RandomAccessPoint point)
    {
        _video.Add(point);
    }

    public void AddAudio(in RandomAccessPoint point)
    {
        _audio.Add(point);
    }

    /// <summary>Writes mfra (with mfro) into <paramref name="buffer"/>, emptied first; <paramref name="withAudio"/>: a tfra for the sound too.</summary>
    public void Write(BoxBuffer buffer, bool withAudio)
    {
        buffer.Reset();

        var index = buffer.Begin(Mp4BoxType.FragmentRandomAccess);
        WriteTrack(buffer, InitSegment.VideoTrackId, _video);

        if (withAudio)
        {
            WriteTrack(buffer, InitSegment.AudioTrackId, _audio);
        }

        var offset = buffer.BeginFull(Mp4BoxType.FragmentRandomAccessOffset, 0, 0);
        buffer.U32((uint)(buffer.Length + sizeof(uint)));
        buffer.End(offset);
        buffer.End(index);
    }

    private static void WriteTrack(BoxBuffer buffer, int trackId, List<RandomAccessPoint> points)
    {
        var box = buffer.BeginFull(Mp4BoxType.TrackFragmentRandomAccess, VersionLong, 0);
        buffer.U32((uint)trackId);
        buffer.U32(FieldSizes);
        buffer.U32((uint)points.Count);

        foreach (var point in points)
        {
            buffer.U64((ulong)point.Time);
            buffer.U64((ulong)point.FragmentOffset);
            buffer.U8(point.TrackFragment);
            buffer.U8(FirstRun);
            buffer.U32((uint)point.Sample);
        }

        buffer.End(box);
    }
}
