// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Relay.Media.Windows.Recording;
using Xunit.Abstractions;

namespace Phantom.Relay.Media.Windows.Tests.Recording;

/// <summary>
/// An H.264 stream of the console's shape, made at test time by Windows' encoder, remuxed into an MP4 file without
/// re-encoding and read back with Media Foundation's source reader: every access unit a sample, the size and the length
/// the units' times give.
/// </summary>
[Trait("Category", "Live")]
public sealed class Mp4H264RemuxerLiveTests
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int Frames = 60;
    private const int FramesPerSecond = 60;

    private readonly ITestOutputHelper _output;

    public Mp4H264RemuxerLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void A_synthetic_stream_is_remuxed_unit_for_unit_and_reads_back_whole()
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Frames);
        var path = RecordingTestFolder.File(nameof(Mp4H264RemuxerLiveTests), "source.mp4");

        var started = Stopwatch.GetTimestamp();
        Remux(path, units);
        var spent = Stopwatch.GetElapsedTime(started);
        var facts = Mp4Reader.TryRead(path, out var problem);

        Assert.True(facts is not null, problem);
        _output.WriteLine($"{facts}; {facts.MegabitsPerSecond:F1} Mbit/s; remuxed in {spent.TotalMilliseconds:F1} ms ({spent.TotalMilliseconds / Frames:F3} ms a unit)");
        Assert.Equal((Frames, Width, Height, "H.264"), (facts.Frames, facts.Width, facts.Height, facts.Codec));
        Assert.True(facts.KeyFrames >= 1);
        Assert.InRange(facts.Duration.TotalSeconds, 0.98, 1.02);
    }

    [Fact]
    public void A_first_unit_without_parameter_sets_is_refused_with_a_reason()
    {
        var path = RecordingTestFolder.File(nameof(Mp4H264RemuxerLiveTests), "refused.mp4");

        var remuxer = Mp4H264Remuxer.TryCreate(path, Width, Height, FramesPerSecond, [0, 0, 0, 1, 0x65, 0x88], out var problem);

        Assert.Null(remuxer);
        Assert.Contains("parameter sets", problem, StringComparison.Ordinal);
    }

    private static void Remux(string path, List<byte[]> units)
    {
        using var remuxer = Mp4H264Remuxer.TryCreate(path, Width, Height, FramesPerSecond, units[0], out var problem);
        Assert.True(remuxer is not null, problem);

        for (var index = 0; index < units.Count; index++)
        {
            var key = AnnexBUnits.IsKeyFrame(units[index]);
            Assert.True(remuxer.TryWrite(units[index], index * SyntheticH264Encoding.FrameUnits, SyntheticH264Encoding.FrameUnits, key));
        }

        Assert.True(remuxer.TryFinish(out problem), problem);
        Assert.Equal(units.Count, remuxer.Written);
    }
}
