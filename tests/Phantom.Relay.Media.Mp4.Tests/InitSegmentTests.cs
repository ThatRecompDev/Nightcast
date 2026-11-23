// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

public sealed class InitSegmentTests
{
    private static readonly Mp4AudioTrack Stereo = new(2, 312);

    private static readonly int[] Brands = [8, 12, 16];

    [Fact]
    public void The_front_is_ftyp_then_moov_written_at_once()
    {
        using var file = new WrittenFile(Stereo);

        Assert.Equal(["ftyp", "moov"], file.Boxes.Select(box => box.Type));

        var brands = file.Boxes[0];
        Assert.Equal("isom", Fourcc(brands.U32(0)));
        Assert.Equal(["isom", "iso6", "mp41"], Brands.Select(at => Fourcc(brands.U32(at))));
    }

    [Fact]
    public void The_movie_has_a_header_two_tracks_and_says_it_is_fragmented()
    {
        var movie = WrittenFile.Front(Stereo);

        Assert.Equal(["mvhd", "trak", "trak", "mvex"], movie.Children().Select(box => box.Type));
        Assert.Equal(1000u, movie.Child("mvhd").U32(12));
        Assert.Equal(3u, movie.Child("mvhd").U32(96));

        var extends = movie.Child("mvex");
        Assert.Equal(["mehd", "trex", "trex"], extends.Children().Select(box => box.Type));
        Assert.Equal(0ul, extends.Child("mehd").U64(4));
        Assert.Equal([1u, 2u], extends.Children().Where(box => box.Type == "trex").Select(box => box.U32(4)));
    }

    [Fact]
    public void Without_sound_there_is_one_track()
    {
        var movie = WrittenFile.Front(null);

        Assert.Single(movie.Children(), box => box.Type == "trak");
        Assert.Single(movie.Child("mvex").Children(), box => box.Type == "trex");
    }

    [Fact]
    public void The_video_track_is_h264_at_90_khz_at_the_pictures_size()
    {
        var track = WrittenFile.Front(Stereo).Children().First(box => box.Type == "trak");

        var header = track.Child("tkhd");
        Assert.Equal(1u, header.U32(12));
        Assert.Equal(1280u << 16, header.U32(76));
        Assert.Equal(720u << 16, header.U32(80));
        Assert.Equal(90000u, track.At("mdia/mdhd").U32(12));
        Assert.Equal("vide", Fourcc(track.At("mdia/hdlr").U32(8)));

        var entry = Assert.Single(track.At("mdia/minf/stbl/stsd").After(8));
        Assert.Equal("avc1", entry.Type);
        Assert.Equal(1280, entry.U16(24));
        Assert.Equal(720, entry.U16(26));
    }

    [Fact]
    public void The_avcC_carries_the_profile_level_and_both_parameter_sets()
    {
        var track = WrittenFile.Front(Stereo).Children().First(box => box.Type == "trak");
        var entry = Assert.Single(track.At("mdia/minf/stbl/stsd").After(8));
        var configuration = Assert.Single(entry.After(78));

        byte[] expected = [1, 0x64, 0x00, 0x1F, 0xFF, 0xE1, 0x00, (byte)SyntheticMedia.Sps.Length, .. SyntheticMedia.Sps, 1, 0x00, (byte)SyntheticMedia.Pps.Length, .. SyntheticMedia.Pps];
        Assert.Equal("avcC", configuration.Type);
        Assert.Equal(expected, configuration.Payload.ToArray());
    }

    [Fact]
    public void The_sound_track_is_opus_at_48_khz_with_its_dOps()
    {
        var track = WrittenFile.Front(Stereo).Children().Last(box => box.Type == "trak");

        Assert.Equal(2u, track.Child("tkhd").U32(12));
        Assert.Equal(48000u, track.At("mdia/mdhd").U32(12));
        Assert.Equal("soun", Fourcc(track.At("mdia/hdlr").U32(8)));

        var entry = Assert.Single(track.At("mdia/minf/stbl/stsd").After(8));
        Assert.Equal("Opus", entry.Type);
        Assert.Equal(2, entry.U16(16));
        Assert.Equal(16, entry.U16(18));
        Assert.Equal(48000u << 16, entry.U32(24));

        var specific = Assert.Single(entry.After(28));
        Assert.Equal("dOps", specific.Type);
        Assert.Equal(new byte[] { 0, 2, 0x01, 0x38, 0x00, 0x00, 0xBB, 0x80, 0, 0, 0 }, specific.Payload.ToArray());
    }

    [Fact]
    public void The_sample_tables_are_empty()
    {
        var table = WrittenFile.Front(Stereo).Children().First(box => box.Type == "trak").At("mdia/minf/stbl");

        Assert.Equal(["stsd", "stts", "stsc", "stco", "stsz"], table.Children().Select(box => box.Type));
        Assert.Equal(0u, table.Child("stts").U32(4));
        Assert.Equal(0u, table.Child("stsz").U32(8));
    }

    private static string Fourcc(uint value)
    {
        return new string([(char)(value >> 24), (char)((value >> 16) & 0xFF), (char)((value >> 8) & 0xFF), (char)(value & 0xFF)]);
    }
}
