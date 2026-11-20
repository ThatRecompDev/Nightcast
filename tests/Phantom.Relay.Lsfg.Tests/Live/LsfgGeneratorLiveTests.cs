// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Xunit.Abstractions;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// The frame generation on this machine's GPU with the user's own Lossless.dll (found through Steam, or the
/// LSFG_LOSSLESS_DLL variable): reflects its shaders, builds the pipeline at 2560 x 1440, generates the frame between
/// two synthetic frames (a textured gradient that moves 32 pixels and brightens), checks it is plausible and prints the GPU time.
/// </summary>
[Trait("Category", "Live")]
public sealed class LsfgGeneratorLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int Shift = 32;
    private const int Brighten = 30;
    private const int Timed = 20;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public LsfgGeneratorLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void The_users_Lossless_dll_holds_every_frame_generation_shader()
    {
        using var catalog = OpenCatalog();

        for (var id = 255; id <= 302; id++)
        {
            Assert.True(catalog.Shaders.TryGetValue(id, out var layout), $"no compute shader {id}");
            _output.WriteLine($"{id}: {layout.GroupWidth}x{layout.GroupHeight}, {layout.Textures} in, {layout.UnorderedAccesses} out, {layout.Samplers} samplers, cb {layout.ConstantBufferSize} bytes, reads {string.Join(' ', layout.Constants.Where(static c => c.Used).Select(static c => c.Name))}");
        }
    }

    [Theory]
    [InlineData(false, 1f)]
    [InlineData(true, 1f)]
    [InlineData(false, 0.5f)]
    public void The_frame_between_two_1440p_frames_is_plausible(bool performance, float flowScale)
    {
        using var catalog = OpenCatalog();
        using var device = new D3D11TestDevice(0);
        var previousPixels = Picture(0, 0);
        var currentPixels = Picture(Shift, Brighten);

        var previous = device.CreateRgba(Width, Height, previousPixels);
        var current = device.CreateRgba(Width, Height, currentPixels);
        var settings = LsfgSettings.Default with { Enabled = true, PerformanceMode = performance, FlowScale = flowScale };

        using var generator = LsfgGenerator.TryCreate(device.Device, catalog, settings, Width, Height, LsfgFrameFormat.Rgba8, _log, out var problem);
        Assert.True(generator is not null, problem);

        var outputs = new nint[1];
        Assert.Equal(1, generator.Generate(previous, current, 2, outputs));
        var generated = device.ReadRgba(outputs[0], Width, Height);

        var (before, after, between) = (Mean(previousPixels), Mean(currentPixels), Mean(generated));
        _output.WriteLine($"mean luma: previous {before:F2}, generated {between:F2}, current {after:F2}");
        _output.WriteLine($"mean difference: to previous {Difference(generated, previousPixels):F2}, to current {Difference(generated, currentPixels):F2}, previous to current {Difference(previousPixels, currentPixels):F2}");

        var ideal = Picture(Shift / 2, Brighten / 2);
        _output.WriteLine($"to the ideal middle frame (moved {Shift / 2} px): generated {Difference(generated, ideal):F2}, a plain 50/50 blend {Difference(Blend(previousPixels, currentPixels), ideal):F2}");
        Assert.True(between > 8, $"the generated frame is black (mean {between:F2})");
        Assert.InRange(between, Math.Min(before, after) - 1, Math.Max(before, after) + 1);
        Assert.True(Difference(generated, ideal) < Difference(Blend(previousPixels, currentPixels), ideal), "the generated frame is no closer to the moved middle frame than a plain blend");

        Time(generator, previous, current, outputs);
    }

    private static LsfgShaderCatalog CreateCatalog(string path, TestOutputLogger log)
    {
        var catalog = LsfgShaderCatalog.TryLoad(path, log, out var problem);
        Assert.True(catalog is not null, problem);

        return catalog;
    }

    // A horizontal gradient with value noise on it (24-pixel cells, so motion is unambiguous), moved right and brightened.
    private static byte[] Picture(int shift, int brighten)
    {
        var pixels = new byte[Width * Height * 4];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var source = x - shift;
                var value = Math.Clamp(20 + (source * 100 / Width) + (Noise(source, y) * 0.6) + brighten, 0, 255);
                var offset = ((y * Width) + x) * 4;

                pixels[offset] = (byte)value;
                pixels[offset + 1] = (byte)value;
                pixels[offset + 2] = (byte)(value / 2);
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    // Bilinear value noise over a hashed grid of 24-pixel cells, 0 to 190.
    private static double Noise(int x, int y)
    {
        const double Cell = 24;
        var cellX = (int)Math.Floor(x / Cell);
        var cellY = (int)Math.Floor(y / Cell);

        var u = (x / Cell) - cellX;
        var v = (y / Cell) - cellY;
        var top = (Hash(cellX, cellY) * (1 - u)) + (Hash(cellX + 1, cellY) * u);
        var bottom = (Hash(cellX, cellY + 1) * (1 - u)) + (Hash(cellX + 1, cellY + 1) * u);

        return (top * (1 - v)) + (bottom * v);
    }

    private static double Hash(int x, int y)
    {
        var hash = (uint)((x * 73856093) ^ (y * 19349663));
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;

        return hash % 190;
    }

    private static double Mean(byte[] pixels)
    {
        long sum = 0;

        for (var index = 0; index < pixels.Length; index += 4)
        {
            sum += (pixels[index] * 299) + (pixels[index + 1] * 587) + (pixels[index + 2] * 114);
        }

        return sum / 1000.0 / (pixels.Length / 4);
    }

    private static byte[] Blend(byte[] first, byte[] second)
    {
        var blend = new byte[first.Length];

        for (var index = 0; index < first.Length; index++)
        {
            blend[index] = (byte)((first[index] + second[index] + 1) / 2);
        }

        return blend;
    }

    private static double Difference(byte[] first, byte[] second)
    {
        long sum = 0;

        for (var index = 0; index < first.Length; index += 4)
        {
            sum += Math.Abs(first[index] - second[index]);
        }

        return sum / (double)(first.Length / 4);
    }

    private LsfgShaderCatalog OpenCatalog()
    {
        var path = Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL");
        var found = LosslessLocator.Find(path, LosslessLocator.SteamLibraries());
        Assert.True(found is not null, "Lossless.dll was not found in the Steam libraries; set LSFG_LOSSLESS_DLL");

        return CreateCatalog(found, _log);
    }

    // The GPU time of one real frame's passes and of one generated frame, averaged after a warm-up.
    private void Time(LsfgGenerator generator, nint previous, nint current, nint[] outputs)
    {
        using var timer = new GpuTimer(generator.Device);
        var submit = new List<double>();
        var generate = new List<double>();

        for (var run = 0; run < Timed + 3; run++)
        {
            var frame = run % 2 == 0 ? previous : current;
            timer.Begin();
            generator.Submit(frame);
            timer.End();
            var submitted = Wait(timer);

            timer.Begin();
            _ = generator.Generate(2, outputs);
            timer.End();
            var generated = Wait(timer);

            if (run >= 3)
            {
                submit.Add(submitted);
                generate.Add(generated);
            }
        }

        _output.WriteLine($"GPU per real frame: average {submit.Average():F3} ms, worst {submit.Max():F3} ms");
        _output.WriteLine($"GPU per generated frame: average {generate.Average():F3} ms, worst {generate.Max():F3} ms (flow {generator.FlowWidth}x{generator.FlowHeight})");
    }

    private static double Wait(GpuTimer timer)
    {
        var clock = Stopwatch.StartNew();
        double milliseconds;

        while (!timer.TryRead(out milliseconds))
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), "the GPU never reached the end of the timed work");
            Thread.Yield();
        }

        return milliseconds;
    }
}
