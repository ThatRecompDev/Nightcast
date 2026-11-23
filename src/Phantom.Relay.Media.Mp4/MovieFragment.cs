// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// A movie fragment's header (ISO/IEC 14496-12 8.8): moof with its sequence number and a traf for each track that has
/// samples (tfhd with the moof as the data's base, tfdt with the run's first decoding time, a trun with each sample's
/// duration and size, and for video its flags), then the mdat's header. The mdat's bytes, the video's then the
/// sound's, follow it as the runs hold them; each trun's data offset points at its track's part from the moof's start.
/// </summary>
internal static class MovieFragment
{
    /// <summary>A sample that depends on no other: a key frame, or any Opus packet (sample_depends_on 2).</summary>
    public const uint SyncFlags = 0x02000000;

    /// <summary>A sample that depends on others and is no sync sample (sample_depends_on 1, sample_is_non_sync_sample).</summary>
    public const uint NonSyncFlags = 0x01010000;

    /// <summary>tfhd's default-base-is-moof: data offsets count from the moof's first byte.</summary>
    public const uint BaseIsMoof = 0x020000;

    /// <summary>trun's flags: a data offset, then each sample's duration and size.</summary>
    public const uint RunOffsetDurationSize = 0x000301;

    /// <summary>trun's flag for each sample's own flags.</summary>
    public const uint RunSampleFlags = 0x000400;

    private const int VersionLong = 1;

    /// <summary>
    /// Writes into <paramref name="header"/> (emptied first) fragment <paramref name="sequence"/> of what
    /// <paramref name="video"/> and <paramref name="audio"/> hold, up to its mdat's header.
    /// </summary>
    public static void Write(BoxBuffer header, int sequence, SampleRun video, SampleRun? audio)
    {
        header.Reset();

        var fragment = header.Begin(Mp4BoxType.MovieFragment);
        var sequenceBox = header.BeginFull(Mp4BoxType.MovieFragmentHeader, 0, 0);
        header.U32((uint)sequence);
        header.End(sequenceBox);
        var videoOffset = WriteTrack(header, InitSegment.VideoTrackId, video, perSampleFlags: true);
        var audioOffset = -1;

        if (audio is not null)
        {
            audioOffset = WriteTrack(header, InitSegment.AudioTrackId, audio, perSampleFlags: false);
        }

        header.End(fragment);
        var dataStart = header.Length + BoxBuffer.HeaderLength;
        PatchOffset(header, videoOffset, dataStart);
        PatchOffset(header, audioOffset, dataStart + video.CompleteLength);

        var mediaLength = BoxBuffer.HeaderLength + video.CompleteLength + (audio?.CompleteLength ?? 0);
        header.U32((uint)mediaLength);
        header.U32(Mp4BoxType.MediaData);
    }

    // A traf for a run with samples; where its data offset is, or -1 for none.
    private static int WriteTrack(BoxBuffer header, int trackId, SampleRun run, bool perSampleFlags)
    {
        if (run.Count == 0)
        {
            return -1;
        }

        var traf = header.Begin(Mp4BoxType.TrackFragment);
        var trackHeader = header.BeginFull(Mp4BoxType.TrackFragmentHeader, 0, BaseIsMoof);
        header.U32((uint)trackId);
        header.End(trackHeader);

        var decodeTime = header.BeginFull(Mp4BoxType.TrackFragmentDecodeTime, VersionLong, 0);
        header.U64((ulong)run.BaseTime);
        header.End(decodeTime);

        var offset = WriteRun(header, run, perSampleFlags);
        header.End(traf);

        return offset;
    }

    private static int WriteRun(BoxBuffer header, SampleRun run, bool perSampleFlags)
    {
        var flags = RunOffsetDurationSize;

        if (perSampleFlags)
        {
            flags |= RunSampleFlags;
        }

        var box = header.BeginFull(Mp4BoxType.TrackRun, 0, flags);
        header.U32((uint)run.Count);
        var offset = header.Length;
        header.I32(0);

        foreach (var sample in run.Samples)
        {
            header.U32(sample.Duration);
            header.U32((uint)sample.Size);

            if (perSampleFlags)
            {
                header.U32(sample.Flags);
            }
        }

        header.End(box);

        return offset;
    }

    private static void PatchOffset(BoxBuffer header, int at, int value)
    {
        if (at >= 0)
        {
            header.PatchU32(at, (uint)value);
        }
    }
}
