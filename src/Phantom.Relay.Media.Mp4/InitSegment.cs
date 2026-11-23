// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// The front of a fragmented MP4 (ISO/IEC 14496-12): ftyp, then a moov that describes the tracks (the video as track
/// <see cref="VideoTrackId"/>, the sound as <see cref="AudioTrackId"/>) with empty sample tables, and an mvex that says
/// the samples are in the fragments after it. Its mehd holds the movie's whole length, 0 until the writer finishes
/// (<see cref="Write"/> says where it is), so a file cut short is still a valid one of unknown length.
/// </summary>
internal static class InitSegment
{
    public const int VideoTrackId = 1;
    public const int AudioTrackId = 2;

    /// <summary>The movie's time units a second (mvhd, mehd).</summary>
    public const int MovieTimescale = 1000;

    private const int MinorVersion = 0x200;
    private const int VersionLong = 1;

    // A version 0 header's creation and modification times (unknown: zero); hdlr's three reserved words.
    private const int TimesBytes = 8;
    private const int ReservedHandlerBytes = 12;

    // tkhd's reserved word and duration (none: the fragments have it).
    private const int IdleTrackBytes = 8;

    // trex's default duration, size and flags: none (each trun gives its own).
    private const int TrackDefaultsBytes = 12;

    // mvhd's rate (1.0, 16.16) and volume (1.0, 8.8), and a track's volume (sound full, video none).
    private const uint UnitRate = 0x00010000;
    private const int UnitVolume = 0x0100;
    private const int ReservedHeaderBytes = 10;
    private const int PreDefinedHeaderBytes = 24;

    // tkhd's flags: enabled, in the movie, in the preview.
    private const uint TrackEnabled = 0x7;
    private const int ReservedTrackBytes = 8;
    private const int SizeShift = 16;

    // mdhd's language: 'und' packed as three 5-bit letters.
    private const int Undetermined = 0x55C4;

    // The unity transformation matrix (16.16, 16.16, 2.30).
    private static readonly uint[] Matrix = [0x00010000, 0, 0, 0, 0x00010000, 0, 0, 0, 0x40000000];

    /// <summary>
    /// Writes the segment for <paramref name="video"/> and <paramref name="audio"/> (null: no sound) into
    /// <paramref name="buffer"/>; where its mehd's duration is in it.
    /// </summary>
    public static int Write(BoxBuffer buffer, Mp4VideoTrack video, Mp4AudioTrack? audio)
    {
        WriteFileType(buffer);

        var nextTrack = AudioTrackId;

        if (audio is not null)
        {
            nextTrack++;
        }

        var movie = buffer.Begin(Mp4BoxType.Movie);
        WriteMovieHeader(buffer, nextTrack);
        VideoTrack.Write(buffer, video);

        if (audio is not null)
        {
            SoundTrack.Write(buffer, audio);
        }

        var duration = WriteExtends(buffer, audio is not null);
        buffer.End(movie);

        return duration;
    }

    /// <summary>A track's tkhd: its id, no length (the fragments have it), its volume and its size (16.16).</summary>
    public static void WriteTrackHeader(BoxBuffer buffer, int id, int volume, int width, int height)
    {
        var box = buffer.BeginFull(Mp4BoxType.TrackHeader, 0, TrackEnabled);
        buffer.Zeros(TimesBytes);
        buffer.U32((uint)id);
        buffer.Zeros(IdleTrackBytes);
        buffer.Zeros(ReservedTrackBytes);
        buffer.U32(0);

        buffer.U16(volume);
        buffer.U16(0);
        WriteMatrix(buffer);
        buffer.U32((uint)width << SizeShift);
        buffer.U32((uint)height << SizeShift);
        buffer.End(box);
    }

    /// <summary>A track's mdhd (its timescale, no length) and hdlr (video or sound, and the handler's name).</summary>
    public static void WriteMediaHeaders(BoxBuffer buffer, int timescale, uint handler, string name)
    {
        var header = buffer.BeginFull(Mp4BoxType.MediaHeader, 0, 0);
        buffer.Zeros(TimesBytes);
        buffer.U32((uint)timescale);
        buffer.U32(0);

        buffer.U16(Undetermined);
        buffer.U16(0);
        buffer.End(header);

        var handlerBox = buffer.BeginFull(Mp4BoxType.Handler, 0, 0);
        buffer.U32(0);
        buffer.U32(handler);
        buffer.Zeros(ReservedHandlerBytes);
        WriteName(buffer, name);
        buffer.End(handlerBox);
    }

    /// <summary>dinf with its one data reference (this file), and the empty sample tables around <paramref name="entry"/>.</summary>
    public static void WriteDataAndSamples(BoxBuffer buffer, Action<BoxBuffer> entry)
    {
        var information = buffer.Begin(Mp4BoxType.DataInformation);
        var references = buffer.BeginFull(Mp4BoxType.DataReference, 0, 0);
        buffer.U32(1);
        buffer.End(buffer.BeginFull(Mp4BoxType.DataEntryUrl, 0, 1));
        buffer.End(references);
        buffer.End(information);

        var table = buffer.Begin(Mp4BoxType.SampleTable);
        var descriptions = buffer.BeginFull(Mp4BoxType.SampleDescription, 0, 0);
        buffer.U32(1);
        entry(buffer);
        buffer.End(descriptions);
        WriteEmptyTables(buffer);

        buffer.End(table);
    }

    private static void WriteFileType(BoxBuffer buffer)
    {
        var box = buffer.Begin(Mp4BoxType.FileType);
        buffer.U32(Mp4BoxType.BrandIsom);
        buffer.U32(MinorVersion);

        buffer.U32(Mp4BoxType.BrandIsom);
        buffer.U32(Mp4BoxType.BrandIso6);
        buffer.U32(Mp4BoxType.BrandMp41);
        buffer.End(box);
    }

    private static void WriteMovieHeader(BoxBuffer buffer, int nextTrack)
    {
        var box = buffer.BeginFull(Mp4BoxType.MovieHeader, 0, 0);
        buffer.Zeros(TimesBytes);
        buffer.U32(MovieTimescale);
        buffer.U32(0);
        buffer.U32(UnitRate);
        buffer.U16(UnitVolume);

        buffer.Zeros(ReservedHeaderBytes);
        WriteMatrix(buffer);
        buffer.Zeros(PreDefinedHeaderBytes);
        buffer.U32((uint)nextTrack);
        buffer.End(box);
    }

    // mvex: mehd (its duration filled in at the end), then each track's trex (sample description 1, no defaults).
    private static int WriteExtends(BoxBuffer buffer, bool withAudio)
    {
        var box = buffer.Begin(Mp4BoxType.MovieExtends);
        var header = buffer.BeginFull(Mp4BoxType.MovieExtendsHeader, VersionLong, 0);
        var duration = buffer.Length;
        buffer.U64(0);
        buffer.End(header);
        WriteTrackExtends(buffer, VideoTrackId);

        if (withAudio)
        {
            WriteTrackExtends(buffer, AudioTrackId);
        }

        buffer.End(box);

        return duration;
    }

    private static void WriteTrackExtends(BoxBuffer buffer, int id)
    {
        var box = buffer.BeginFull(Mp4BoxType.TrackExtends, 0, 0);
        buffer.U32((uint)id);
        buffer.U32(1);
        buffer.Zeros(TrackDefaultsBytes);
        buffer.End(box);
    }

    private static void WriteEmptyTables(BoxBuffer buffer)
    {
        foreach (var type in (ReadOnlySpan<uint>)[Mp4BoxType.TimeToSample, Mp4BoxType.SampleToChunk, Mp4BoxType.ChunkOffset])
        {
            var box = buffer.BeginFull(type, 0, 0);
            buffer.U32(0);
            buffer.End(box);
        }

        var sizes = buffer.BeginFull(Mp4BoxType.SampleSize, 0, 0);
        buffer.U32(0);
        buffer.U32(0);
        buffer.End(sizes);
    }

    private static void WriteMatrix(BoxBuffer buffer)
    {
        foreach (var value in Matrix)
        {
            buffer.U32(value);
        }
    }

    // A null-terminated UTF-8 name; the writer's names are ASCII.
    private static void WriteName(BoxBuffer buffer, string name)
    {
        foreach (var character in name)
        {
            buffer.U8(character);
        }

        buffer.U8(0);
    }
}
