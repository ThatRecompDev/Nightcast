// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Relay.Media.Windows;
using Xunit.Abstractions;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// The frame generation as the decoder's GPU path runs it, on this machine's GPU with the user's own Lossless.dll:
/// NV12 pictures in, the generated frame written back as NV12 into a slot of a shared ring (as the renderer draws it),
/// and the RGBA to NV12 step on its own against known grey levels. Prints the GPU and CPU time a picture.
/// </summary>
[Trait("Category", "Live")]
public sealed class LsfgFrameStageLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int Shift = 32;
    private const int Cell = 24;
    private const int Timed = 60;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public LsfgFrameStageLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void Rgba_goes_back_to_limited_range_nv12_in_a_shared_slot()
    {
        using var device = new D3D11TestDevice(0);
        var nv12 = device.CreateNv12(Width, Height, Halves(16, 235));
        using var toRgba = Nv12ToRgbaConverter.TryCreate(device.Device, Width, Height, out var problem);
        using var toNv12 = RgbaToNv12Converter.TryCreate(device.Device, Width, Height, out problem);
        using var ring = SharedSurfaceRing.TryCreate(device.Device, 1, Width, Height, out problem);

        Assert.True(toRgba is not null && toNv12 is not null && ring is not null, problem);
        Assert.True(ring.IsWritableByVideoProcessor);

        Assert.True(toRgba.Convert(nv12, 0));
        Assert.True(toNv12.Convert(toRgba.Output, ring.Texture(0)));
        var luma = device.ReadLuma(ring.Texture(0), Width, Height);

        Assert.InRange(luma[(Height / 2 * Width) + (Width / 4)], 14, 18);
        Assert.InRange(luma[(Height / 2 * Width) + (Width * 3 / 4)], 233, 237);
    }

    [Fact]
    public void A_frame_is_generated_between_two_pictures_and_written_into_a_shared_slot()
    {
        using var device = new D3D11TestDevice(0);
        var previous = device.CreateNv12(Width, Height, Moving(0));
        var current = device.CreateNv12(Width, Height, Moving(Shift));
        var settings = LsfgSettings.Default with { Enabled = true, LibraryPath = Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL") };
        using var stage = LsfgFrameStage.TryCreate(device.Device, settings, Width, Height, _log, out var problem);
        using var ring = SharedSurfaceRing.TryCreate(device.Device, 1, Width, Height, out problem);

        Assert.True(stage is not null && ring is not null, problem);

        var generated = new nint[LsfgSettings.MaximumMultiplier - 1];
        Assert.Equal(0, Picture(stage, previous, generated, ring));
        Assert.Equal(1, Picture(stage, current, generated, ring));
        var luma = device.ReadLuma(ring.Texture(0), Width, Height);

        var (before, between, after) = (Mean(Moving(0)), Mean(luma), Mean(Moving(Shift)));
        _output.WriteLine($"mean luma: previous {before:F1}, generated {between:F1}, current {after:F1}");
        Assert.InRange(between, Math.Min(before, after) - 4, Math.Max(before, after) + 4);
        Time(stage, previous, current, generated, ring);
    }

    // One new picture through the stage, its generated frames written into the slot.
    private static int Picture(LsfgFrameStage stage, nint picture, nint[] generated, SharedSurfaceRing ring)
    {
        var count = stage.Process(picture, 0, generated);

        for (var index = 0; index < count; index++)
        {
            Assert.True(stage.Write(generated[index], ring.Texture(0)));
        }

        stage.Done();
        ring.Signal();

        return count;
    }

    private void Time(LsfgFrameStage stage, nint previous, nint current, nint[] generated, SharedSurfaceRing ring)
    {
        var started = Stopwatch.GetTimestamp();

        for (var index = 0; index < Timed; index++)
        {
            var picture = previous;

            if (index % 2 == 1)
            {
                picture = current;
            }

            Picture(stage, picture, generated, ring);
        }

        var cpu = Stopwatch.GetElapsedTime(started).TotalMilliseconds / Timed;
        _output.WriteLine($"x2 quality at {Width}x{Height}: GPU {stage.LastGpuMilliseconds:F2} ms a picture (NV12 to RGBA, its passes, the generated frame, RGBA to NV12), CPU {cpu:F3} ms a picture to queue it");
    }

    private static byte[] Halves(byte left, byte right)
    {
        var planes = new byte[Width * Height * 3 / 2];

        for (var y = 0; y < Height; y++)
        {
            planes.AsSpan(y * Width, Width / 2).Fill(left);
            planes.AsSpan((y * Width) + (Width / 2), Width / 2).Fill(right);
        }

        planes.AsSpan(Width * Height).Fill(128);

        return planes;
    }

    // Blocks of value noise on a gradient, moved right by shift pixels; chroma neutral.
    private static byte[] Moving(int shift)
    {
        var planes = new byte[Width * Height * 3 / 2];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cellX = (x - shift + Width) / Cell;
                var noise = (int)((uint)((cellX * 73856093) ^ (y / Cell * 19349663)) % 64);
                planes[(y * Width) + x] = (byte)(40 + (x * 100 / Width) + noise);
            }
        }

        planes.AsSpan(Width * Height).Fill(128);

        return planes;
    }

    private static double Mean(byte[] luma)
    {
        long sum = 0;

        for (var index = 0; index < Width * Height; index++)
        {
            sum += luma[index];
        }

        return sum / (double)(Width * Height);
    }
}
