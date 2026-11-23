// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

/// <summary>
/// The writer on a real H.264 and Opus stream that ffmpeg made from its test sources (never a capture): the folder
/// <c>RELAY_MP4_SYNTHETIC_DIR</c> names holds <c>video.h264</c> (x264 with aud=1, no B-frames, 60 fps) and
/// <c>audio.opus</c> (Ogg, 48 kHz stereo); the test writes <c>synthetic.mp4</c> beside them, with the sound starting
/// 50 ms after the video and one packet lost, for ffprobe and ffmpeg to check (docs/architecture/recorder.md).
/// </summary>
public sealed class SyntheticFileLiveTests
{
    public const string Variable = "RELAY_MP4_SYNTHETIC_DIR";

    private const int Frame = 1500;
    private const int AudioOffset = 2400;
    private const int LostPacket = 100;

    [Fact]
    [Trait("Category", "Live")]
    public void Writes_a_file_from_ffmpegs_streams()
    {
        var folder = Environment.GetEnvironmentVariable(Variable);

        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        var units = AnnexBAccessUnits.Split(File.ReadAllBytes(Path.Combine(folder, "video.h264")));
        var packets = OggPackets.Read(File.ReadAllBytes(Path.Combine(folder, "audio.opus"))).Skip(2).ToList();
        var track = Mp4VideoTrack.FromKeyFrame(units[0].Unit, 1280, 720);
        Assert.NotNull(track);
        Assert.True(units[0].Key);

        using var output = File.Create(Path.Combine(folder, "synthetic.mp4"));
        var writer = new FragmentedMp4Writer(output, track, new Mp4AudioTrack(2, 0));
        var audioTime = (long)AudioOffset;
        var next = 0;

        for (var index = 0; index < units.Count; index++)
        {
            writer.WriteVideo(units[index].Unit, (long)index * Frame, units[index].Key);

            // The sound up to this frame's time on the 48 kHz clock, as it would arrive beside the video.
            for (; next < packets.Count && audioTime <= (long)index * Frame * 48000 / 90000; next++)
            {
                if (next != LostPacket)
                {
                    writer.WriteAudio(packets[next], audioTime);
                }

                audioTime += OpusPackets.Samples(packets[next]);
            }
        }

        var counts = writer.Finish();
        Assert.Equal(units.Count, counts.VideoSamples);
        Assert.Equal(1, counts.AudioConcealed);
    }
}
