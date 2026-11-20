// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;
using Phantom.Relay.Media.Windows.Recording;
using Xunit.Abstractions;

namespace Phantom.Relay.Media.Windows.Tests.Recording;

/// <summary>
/// NV12 textures on a hardware D3D11 device (the decoder's kind, multithread-protected) encoded on the GPU at 1440p and
/// 120 frames a second, then read back: as many frames as were written, the size, the length the times give, and (H.264,
/// which Windows always decodes) each frame decoded again showing the flat luma its texture had, in order (so no pool
/// texture was written while the encoder still read it). Prints the copy's CPU time a frame (what the render thread
/// pays) and the encoder used.
/// </summary>
[Trait("Category", "Live")]
public sealed unsafe class Mp4VideoEncoderLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int FramesPerSecond = 120;
    private const int Frames = 240;
    private const long FrameUnits = 10_000_000 / FramesPerSecond;
    private const int LumaTolerance = 12;

    // Four flat pictures, cycled: a frame shown in the wrong place or overwritten shows another luma.
    private static readonly byte[] Lumas = [40, 90, 140, 190];

    private readonly ITestOutputHelper _output;

    public Mp4VideoEncoderLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(RecordedVideoCodec.H264)]
    [InlineData(RecordedVideoCodec.Hevc)]
    public void Textures_are_encoded_on_the_gpu_and_read_back_frame_for_frame(RecordedVideoCodec codec)
    {
        var wrappers = new StrategyBasedComWrappers();
        using var device = D3D11VideoDevice.TryCreate(wrappers, 0, out var problem);
        Assert.True(device is not null, problem);
        var sources = Lumas.Select(luma => FlatTexture(wrappers, device.Device, luma)).ToArray();
        var path = RecordingTestFolder.File(nameof(Mp4VideoEncoderLiveTests), $"shown-{codec}.mp4");

        try
        {
            Encode(device.Device, path, codec, sources);
        }
        finally
        {
            for (var index = 0; index < sources.Length; index++)
            {
                ComObjects.Release(ref sources[index]);
            }
        }

        var facts = Mp4Reader.TryRead(path, out problem);
        Assert.True(facts is not null, problem);
        _output.WriteLine($"{facts}; {facts.MegabitsPerSecond:F1} Mbit/s");
        Assert.Equal((Frames, Width, Height), (facts.Frames, facts.Width, facts.Height));
        Assert.InRange(facts.Duration.TotalSeconds, 1.98, 2.02);

        // Windows decodes H.264 itself; HEVC only with its optional extension, so only the H.264 file is decoded again.
        if (codec == RecordedVideoCodec.H264)
        {
            AssertLumas(DecodedLuma.AtCentre(path));
        }
    }

    private void Encode(nint device, string path, RecordedVideoCodec codec, nint[] sources)
    {
        var settings = new VideoEncoderSettings(Width, Height, FramesPerSecond, VideoEncoderSettings.HighBitrate, codec);
        using var encoder = Mp4VideoEncoder.TryCreate(device, path, settings, out var problem);
        Assert.True(encoder is not null, problem);
        _output.WriteLine($"{codec}: {encoder.EncoderName}, hardware {encoder.IsHardware}");
        Assert.True(encoder.IsHardware, $"{encoder.EncoderName} is not a hardware encoder");

        var copies = new List<double>();
        var allocated = 0L;
        var busy = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var copied = encoder.TryCopy(sources[frame % sources.Length], 0, out var slot);
            var spent = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            copies.Add(spent);

            while (!copied)
            {
                busy++;
                Thread.Sleep(1);
                copied = encoder.TryCopy(sources[frame % sources.Length], 0, out slot);
            }

            Assert.True(encoder.TryWrite(slot, frame * FrameUnits, FrameUnits));
        }

        Assert.True(encoder.TryFinish(out problem), problem);
        _output.WriteLine($"copy on the CPU: average {copies.Skip(1).Average():F3} ms, worst {copies.Skip(1).Max():F3} ms; managed allocation {allocated} bytes over {Frames} copies; pool busy {busy} times");
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void The_copy_into_the_pool_costs_the_gpu_a_few_hundredths_of_a_millisecond()
    {
        const int Copies = 30;

        var wrappers = new StrategyBasedComWrappers();
        using var device = D3D11VideoDevice.TryCreate(wrappers, 0, out var problem);
        Assert.True(device is not null, problem);
        var source = FlatTexture(wrappers, device.Device, Lumas[0]);

        var path = RecordingTestFolder.File(nameof(Mp4VideoEncoderLiveTests), "copy-cost.mp4");
        using var encoder = Mp4VideoEncoder.TryCreate(device.Device, path, new VideoEncoderSettings(Width, Height, FramesPerSecond, VideoEncoderSettings.HighBitrate, RecordedVideoCodec.H264), out problem);
        Assert.True(encoder is not null, problem);
        using var stopwatch = new GpuStopwatch(wrappers, device.Device);
        var times = new List<double>();

        for (var copy = 0; copy < Copies; copy++)
        {
            stopwatch.Begin();
            Assert.True(encoder.TryCopy(source, 0, out var slot));
            var time = stopwatch.End();
            encoder.Abandon(slot);

            if (time is not null)
            {
                times.Add(time.Value);
            }
        }

        ComObjects.Release(ref source);
        _output.WriteLine($"GPU time of a {Width}x{Height} copy into the pool: median {times.Order().ElementAt(times.Count / 2):F4} ms, worst {times.Max():F4} ms over {times.Count}");
        Assert.NotEmpty(times);
    }

    private static void AssertLumas(List<byte> decoded)
    {
        Assert.Equal(Frames, decoded.Count);

        for (var frame = 0; frame < decoded.Count; frame++)
        {
            var expected = Lumas[frame % Lumas.Length];
            Assert.True(Math.Abs(decoded[frame] - expected) <= LumaTolerance, $"frame {frame}: luma {decoded[frame]}, its texture had {expected}");
        }
    }

    // An NV12 texture of one luma and neutral chroma.
    private static nint FlatTexture(StrategyBasedComWrappers wrappers, nint device, byte luma)
    {
        const byte Neutral = 128;

        var bytes = new byte[Width * Height * 3 / 2];
        bytes.AsSpan(0, Width * Height).Fill(luma);
        bytes.AsSpan(Width * Height).Fill(Neutral);
        var description = new Texture2DDescription { Width = Width, Height = Height, MipLevels = 1, ArraySize = 1, Format = MediaFoundationValues.Nv12Format, SampleCount = 1, Usage = Direct3D11Values.DefaultUsage, BindFlags = Direct3D11Values.BindShaderResource };
        var api = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        nint texture;

        fixed (byte* memory = bytes)
        {
            var data = new SubresourceData { Memory = (nint)memory, RowPitch = Width, SlicePitch = (uint)bytes.Length };
            Assert.True(api.CreateTexture2D(in description, (nint)(&data), out texture) >= 0);
        }

        ComObjects.Free(api);

        return texture;
    }
}
