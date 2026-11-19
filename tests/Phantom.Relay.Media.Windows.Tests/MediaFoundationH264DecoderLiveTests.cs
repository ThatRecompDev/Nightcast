// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Xunit.Abstractions;

namespace Phantom.Relay.Media.Windows.Tests;

/// <summary>
/// The decoder on this machine's GPU and in software, on a stream of the console's shape (2560 x 1440, Main profile,
/// no B-frames) that Windows' encoder makes at test time; prints the decode and read-back times.
/// </summary>
[Trait("Category", "Live")]
public sealed class MediaFoundationH264DecoderLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int Frames = 30;
    private const long FrameNanoseconds = SyntheticH264Encoding.FrameUnits * 100;

    private readonly ITestOutputHelper _output;

    public MediaFoundationH264DecoderLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void A_1440p_stream_decodes_on_the_gpu_one_picture_per_access_unit()
    {
        using var decoder = Open(preferHardware: true);

        Run(decoder);

        Assert.True(decoder.IsHardwareAccelerated, decoder.FallbackReason);
    }

    [Fact]
    public void The_software_path_decodes_the_same_stream()
    {
        using var decoder = Open(preferHardware: false);

        Run(decoder);

        Assert.False(decoder.IsHardwareAccelerated);
    }

    private static MediaFoundationH264Decoder Open(bool preferHardware)
    {
        var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware, out var problem);
        Assert.True(decoder is not null, problem);

        return decoder;
    }

    private void Run(MediaFoundationH264Decoder decoder)
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Frames);
        Assert.Equal(Frames, units.Count);

        var frame = new VideoFrame();
        var decode = new List<double>();
        var copy = new List<double>();
        var allocated = 0L;

        for (var index = 0; index < units.Count; index++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = decoder.Decode(units[index], index * FrameNanoseconds);
            Assert.True(result == VideoDecodeResult.Picture, $"access unit {index}: {result} {decoder.LastError}");
            Assert.True(decoder.TakePicture(frame, out var timestamp), decoder.LastError);

            if (index > 0)
            {
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }

            decode.Add(decoder.LastDecodeDuration.TotalMilliseconds);
            copy.Add(decoder.LastCopyDuration.TotalMilliseconds);
            Assert.Equal(index * FrameNanoseconds, timestamp);
        }

        AssertPicture(frame, Frames - 1);
        Report(decoder, decode, copy, allocated / (units.Count - 1));
    }

    private static void AssertPicture(VideoFrame frame, int index)
    {
        Assert.Equal((Width, Height, Width / 2, Height / 2), (frame.Width, frame.Height, frame.ChromaWidth, frame.ChromaHeight));
        Assert.Equal(VideoColourSpace.Bt709, frame.Colour.Space);

        foreach (var (x, y) in new[] { (100, 60), (1280, 720), (2400, 1300) })
        {
            var expected = SyntheticH264Encoding.LumaAt(x, y, index);
            var actual = frame.Luma[(y * Width) + x];
            Assert.True(Math.Abs(expected - actual) <= 16, $"luma at {x},{y}: {actual}, the gradient has {expected}");
        }
    }

    private void Report(MediaFoundationH264Decoder decoder, List<double> decode, List<double> copy, long bytesPerFrame)
    {
        var path = decoder.IsHardwareAccelerated ? "DXVA" : "software";
        _output.WriteLine($"{path} decode of {decode.Count} access units at {Width}x{Height} (fallback: {decoder.FallbackReason ?? "none"})");
        _output.WriteLine($"decode: first {decode[0]:F2} ms, average {decode.Average():F2} ms, worst {decode.Max():F2} ms, worst after the first {decode.Skip(1).Max():F2} ms");
        _output.WriteLine($"copy:   first {copy[0]:F2} ms, average {copy.Average():F2} ms, worst {copy.Max():F2} ms, worst after the first {copy.Skip(1).Max():F2} ms");
        _output.WriteLine($"managed allocation per frame after the first: {bytesPerFrame} bytes");
    }
}
