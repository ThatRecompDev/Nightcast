// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;

namespace Phantom.Relay.Media.Mp4.Tests;

public sealed class FragmentedMp4WriterTests
{
    private const uint Sync = 0x02000000;
    private const uint NonSync = 0x01010000;
    private const int Frame = 1500;

    private static readonly Mp4AudioTrack Stereo = new(2, 0);

    [Fact]
    public void Units_become_samples_with_4_byte_lengths_and_no_start_codes()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(0x11), 0, key: true);
        file.Writer.WriteVideo(SyntheticMedia.DeltaUnit(0x22), Frame, key: false);
        file.Writer.Finish();

        var fragment = Assert.Single(file.All("moof"));
        var data = WrittenFile.Data(fragment, WrittenFile.Track(fragment, 1)!, withFlags: true);

        byte[] expected =
        [
            0, 0, 0, 2, 0x09, 0x10,
            0, 0, 0, (byte)SyntheticMedia.Sps.Length, .. SyntheticMedia.Sps,
            0, 0, 0, (byte)SyntheticMedia.Pps.Length, .. SyntheticMedia.Pps,
            0, 0, 0, 5, 0x65, 0x88, 0x84, 0x11, 0x21,
            0, 0, 0, 5, 0x41, 0x9A, 0x22, 0x05, 0x10,
        ];
        Assert.Equal(expected, data);
    }

    [Fact]
    public void Each_unit_lasts_until_the_next_and_the_last_as_long_as_the_one_before()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.WriteVideo(SyntheticMedia.DeltaUnit(2), 1500, key: false);
        file.Writer.WriteVideo(SyntheticMedia.DeltaUnit(3), 4500, key: false);
        file.Writer.Finish();

        var fragment = Assert.Single(file.All("moof"));
        var samples = WrittenFile.Samples(WrittenFile.Track(fragment, 1)!, withFlags: true);

        Assert.Equal([1500u, 3000u, 3000u], samples.Select(sample => sample.Duration));
        Assert.Equal([Sync, NonSync, NonSync], samples.Select(sample => sample.Flags));
    }

    [Fact]
    public void A_fragment_is_written_once_a_second_of_video_is_complete()
    {
        using var file = new WrittenFile(Stereo);

        for (var index = 0; index <= 60; index++)
        {
            file.Writer.WriteVideo(SyntheticMedia.DeltaUnit((byte)index), index * Frame, key: index == 0);
        }

        var fragment = Assert.Single(file.All("moof"));
        var samples = WrittenFile.Samples(WrittenFile.Track(fragment, 1)!, withFlags: true);
        Assert.Equal(60, samples.Count);
        Assert.Equal(90000, samples.Sum(sample => (long)sample.Duration));
        Assert.Equal(0ul, WrittenFile.Track(fragment, 1)!.Child("tfdt").U64(4));
    }

    [Fact]
    public void Fragments_count_up_and_each_starts_where_the_last_ended()
    {
        using var file = new WrittenFile(Stereo);

        for (var index = 0; index < 150; index++)
        {
            file.Writer.WriteVideo(SyntheticMedia.DeltaUnit((byte)index), index * Frame, key: index % 60 == 0);
        }

        file.Writer.Finish();
        var fragments = file.All("moof");

        Assert.Equal([1u, 2u, 3u], fragments.Select(fragment => fragment.Child("mfhd").U32(4)));
        Assert.Equal([0ul, 90000ul, 180000ul], fragments.Select(fragment => WrittenFile.Track(fragment, 1)!.Child("tfdt").U64(4)));
        Assert.Equal(150, fragments.Sum(fragment => WrittenFile.Samples(WrittenFile.Track(fragment, 1)!, withFlags: true).Count));
        Assert.Equal(3, file.All("mdat").Count);
    }

    [Fact]
    public void Each_fragment_is_a_moof_then_its_mdat_and_tfhd_counts_from_the_moof()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 1), 0);
        file.Writer.Finish();

        Assert.Equal(["ftyp", "moov", "moof", "mdat", "mfra"], file.Boxes.Select(box => box.Type));

        var fragment = file.All("moof")[0];
        var media = file.All("mdat")[0];
        var video = WrittenFile.Track(fragment, 1)!;
        Assert.Equal(0x020000u, video.Child("tfhd").U32(0) & 0xFFFFFF);
        Assert.Equal(media.Offset + 8, fragment.Offset + (int)video.Child("trun").U32(8));
    }

    [Fact]
    public void Sound_packets_are_samples_with_their_toc_durations_after_the_video_in_the_mdat()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 0xA1), 4800);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc20Ms, 0xA2), 5280);
        file.Writer.Finish();

        var fragment = Assert.Single(file.All("moof"));
        var sound = WrittenFile.Track(fragment, 2)!;

        Assert.Equal(4800ul, sound.Child("tfdt").U64(4));
        Assert.Equal([(480u, 4u, 0u), (960u, 4u, 0u)], WrittenFile.Samples(sound, withFlags: false));
        Assert.Equal([.. SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 0xA1), .. SyntheticMedia.Packet(SyntheticMedia.Toc20Ms, 0xA2)], WrittenFile.Data(fragment, sound, withFlags: false));
    }

    [Fact]
    public void A_gap_in_the_sound_is_filled_with_packets_a_decoder_conceals()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 1), 0);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 2), 1440);
        var counts = file.Writer.Finish();

        var fragment = Assert.Single(file.All("moof"));
        var sound = WrittenFile.Track(fragment, 2)!;

        Assert.Equal([480u, 480u, 480u, 480u], WrittenFile.Samples(sound, withFlags: false).Select(sample => sample.Duration));
        Assert.Equal([4u, 1u, 1u, 4u], WrittenFile.Samples(sound, withFlags: false).Select(sample => sample.Size));
        Assert.Equal(0xF4 & ~0x03, WrittenFile.Data(fragment, sound, withFlags: false)[4]);
        Assert.Equal(2, counts.AudioConcealed);
    }

    [Fact]
    public void Late_repeated_and_early_packets_and_what_is_not_opus_are_left_out()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 1), -480);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 2), 960);

        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 3), 960);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 4), 1450);
        file.Writer.WriteAudio(ReadOnlySpan<byte>.Empty, 1920);
        var counts = file.Writer.Finish();

        var sound = WrittenFile.Track(Assert.Single(file.All("moof")), 2)!;

        Assert.Equal(2, WrittenFile.Samples(sound, withFlags: false).Count);
        Assert.Equal(2, counts.AudioPackets);
        Assert.Equal(3, counts.AudioSkipped);
    }

    [Fact]
    public void Sound_alone_makes_a_fragment_after_two_seconds()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);

        for (var index = 0; index < 200; index++)
        {
            file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 1), index * 480);
        }

        var fragment = Assert.Single(file.All("moof"));
        Assert.Null(WrittenFile.Track(fragment, 1));
        Assert.Equal(200, WrittenFile.Samples(WrittenFile.Track(fragment, 2)!, withFlags: false).Count);
    }

    [Fact]
    public void Finishing_writes_the_movies_length_and_the_random_access_index()
    {
        using var file = new WrittenFile(Stereo);

        for (var index = 0; index < 150; index++)
        {
            file.Writer.WriteVideo(SyntheticMedia.DeltaUnit(1), index * Frame, key: index == 0 || index == 70);
            file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc20Ms, 1), index * 960);
        }

        var counts = file.Writer.Finish();
        var bytes = file.Bytes;
        var index1 = Assert.Single(file.All("mfra"));

        Assert.Equal(TimeSpan.FromMilliseconds(3000), counts.Duration);
        Assert.Equal(3000ul, file.Boxes[1].At("mvex/mehd").U64(4));
        Assert.Equal((uint)index1.Length, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(bytes.Length - 4)));
        Assert.Equal(["tfra", "tfra", "mfro"], index1.Children().Select(box => box.Type));

        var video = index1.Children()[0];
        var fragments = file.All("moof");
        Assert.Equal(2u, video.U32(12));
        Assert.Equal(0ul, video.U64(16));

        Assert.Equal((ulong)fragments[0].Offset, video.U64(24));
        Assert.Equal(70ul * Frame, video.U64(16 + 22));
        Assert.Equal((ulong)fragments[1].Offset, video.U64(24 + 22));
        Assert.Equal(11u, video.U32(16 + 22 + 18));
    }

    [Fact]
    public void A_file_cut_short_is_whole_fragments_after_its_front()
    {
        using var file = new WrittenFile(Stereo);

        for (var index = 0; index < 100; index++)
        {
            file.Writer.WriteVideo(SyntheticMedia.DeltaUnit(1), index * Frame, key: index == 0);
        }

        Assert.Equal(["ftyp", "moov", "moof", "mdat"], file.Boxes.Select(box => box.Type));
        Assert.Equal(0ul, file.Boxes[1].At("mvex/mehd").U64(4));
    }

    [Fact]
    public void Nothing_is_written_after_it_finished()
    {
        using var file = new WrittenFile(Stereo);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.Finish();

        Assert.True(file.Writer.IsFinished);
        Assert.Throws<ObjectDisposedException>(() => file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), Frame, key: true));
    }

    [Fact]
    public void Without_a_sound_track_packets_are_left_out()
    {
        using var file = new WrittenFile(null);
        file.Writer.WriteVideo(SyntheticMedia.KeyUnit(1), 0, key: true);
        file.Writer.WriteAudio(SyntheticMedia.Packet(SyntheticMedia.Toc10Ms, 1), 0);
        var counts = file.Writer.Finish();

        Assert.Equal(1, counts.AudioSkipped);
        Assert.Null(WrittenFile.Track(Assert.Single(file.All("moof")), 2));
    }
}
