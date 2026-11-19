// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Xunit.Abstractions;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// The third-person case: a still, textured foreground (an ellipse the size of a character's head and shoulders at
/// 1440p) over a textured background that moves sideways, as when the camera turns around a character. Three frames go
/// in (the background moved 0, S and 2S pixels) and the frame between the last two comes out; it is compared with the
/// ideal middle frame (the background moved 1.5 S, the foreground where it is) in three regions: the band around the
/// foreground's edge (where the background is uncovered and covered), the foreground's inside and the background away
/// from it. Each of the graph's unsettled choices (<see cref="LsfgWiring"/>) is run. With LSFG_ARTIFACT_DIR set, the
/// crops around the foreground (generated, ideal, and the difference) are written there as PNG.
/// </summary>
[Trait("Category", "Live")]
public sealed class LsfgArtifactLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int CentreX = 1280;
    private const int CentreY = 760;
    private const int RadiusX = 200;
    private const int RadiusY = 270;
    private const int MinimumBand = 32;
    private const int CropSize = 800;
    private const int DifferenceGain = 4;

    // A slow, a moderate and a fast camera turn at 30 fps, in pixels a frame.
    private static readonly int[] Shifts = [16, 32, 64];

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public LsfgArtifactLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    public static TheoryData<string, bool, int, bool, bool> Wirings => new()
    {
        { "standard (mask two levels finer)", false, LsfgWiring.FinerMask, false, false },
        { "mask of the same size", false, 0, false, false },
        { "mask one level coarser", false, 1, false, false },
        { "mask one level finer", false, -1, false, false },
        { "the finest mask everywhere", false, -LsfgShapes.Levels, false, false },
        { "no mask", false, 0, false, true },
        { "beta newest first", true, LsfgWiring.FinerMask, false, false },
        { "point samplers", false, LsfgWiring.FinerMask, true, false },
    };

    [Theory]
    [MemberData(nameof(Wirings))]
    public void The_frame_between_around_a_still_foreground_over_a_moving_background(string name, bool betaNewestFirst, int maskShift, bool pointSampling, bool withoutMask)
    {
        var path = LosslessLocator.Find(Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL"), LosslessLocator.SteamLibraries());
        Assert.True(path is not null, "Lossless.dll was not found");
        using var catalog = LsfgShaderCatalog.TryLoad(path, _log, out var problem);
        Assert.True(catalog is not null, problem);
        using var device = new D3D11TestDevice();

        var wiring = new LsfgWiring(betaNewestFirst, maskShift, pointSampling, withoutMask);
        var settings = LsfgSettings.Default with { Enabled = true };
        using var generator = LsfgGenerator.TryCreate(device.Device, catalog, settings, Width, Height, LsfgFrameFormat.Rgba8, wiring, _log, out problem);
        Assert.True(generator is not null, problem);

        foreach (var shift in Shifts)
        {
            var generated = Generate(device, generator, shift);
            var ideal = Scene(shift * 3 / 2);
            var errors = Errors(generated, ideal, shift);
            var reference = Errors(Blend(Scene(shift), Scene(2 * shift)), ideal, shift);
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{name}, background {shift} px a frame: mean luma error to the ideal: edge band {errors.Edge:F2}, foreground {errors.Inside:F2}, background {errors.Outside:F2}, all {errors.All:F2} (a plain blend: {reference.Edge:F2}, {reference.Inside:F2}, {reference.Outside:F2}, {reference.All:F2})"));

            Save(name, shift, generated, ideal);

            // The alternatives are measured, not judged; the wiring the stream runs must beat a blend.
            if (wiring == LsfgWiring.Standard)
            {
                Assert.True(errors.All < reference.All, "the generated frame is no closer to the ideal than a blend");
            }
        }
    }

    // Three frames in (the background at 0, S, 2S), the frame between the last two out.
    private static byte[] Generate(D3D11TestDevice device, LsfgGenerator generator, int shift)
    {
        var outputs = new nint[1];
        generator.Submit(device.CreateRgba(Width, Height, Scene(0)));
        generator.Submit(device.CreateRgba(Width, Height, Scene(shift)));
        generator.Submit(device.CreateRgba(Width, Height, Scene(2 * shift)));
        Assert.Equal(1, generator.Generate(2, outputs));

        return device.ReadRgba(outputs[0], Width, Height);
    }

    // The background (value noise in 24-pixel cells on a gradient, moved right by shift) with the still foreground over it.
    private static byte[] Scene(int shift)
    {
        var pixels = new byte[Width * Height * 4];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = ((y * Width) + x) * 4;
                Paint(pixels.AsSpan(offset, 4), x, y, shift);
            }
        }

        return pixels;
    }

    private static void Paint(Span<byte> pixel, int x, int y, int shift)
    {
        pixel[3] = 255;

        if (Distance(x, y) <= 0)
        {
            // Skin-like, with a finer texture of its own: 8-pixel cells.
            var skin = 120 + (Noise(x, y, 8) * 0.4);
            pixel[0] = (byte)Math.Clamp(skin + 40, 0, 255);
            pixel[1] = (byte)Math.Clamp(skin, 0, 255);
            pixel[2] = (byte)Math.Clamp(skin - 30, 0, 255);

            return;
        }

        var source = x - shift;
        var value = 30 + (source * 60 / Width) + (Noise(source, y, 24) * 0.8);
        pixel[0] = (byte)Math.Clamp(value * 0.6, 0, 255);
        pixel[1] = (byte)Math.Clamp(value, 0, 255);
        pixel[2] = (byte)Math.Clamp(value * 0.8, 0, 255);
    }

    // About how far outside the foreground's edge a point is, in pixels (negative inside).
    private static double Distance(int x, int y)
    {
        var u = (x - CentreX) / (double)RadiusX;
        var v = (y - CentreY) / (double)RadiusY;

        return (Math.Sqrt((u * u) + (v * v)) - 1) * Math.Min(RadiusX, RadiusY);
    }

    // Bilinear value noise over a hashed grid of cells, 0 to 190.
    private static double Noise(int x, int y, double cell)
    {
        var cellX = (int)Math.Floor(x / cell);
        var cellY = (int)Math.Floor(y / cell);
        var u = (x / cell) - cellX;
        var v = (y / cell) - cellY;

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

    private static byte[] Blend(byte[] first, byte[] second)
    {
        var blend = new byte[first.Length];

        for (var index = 0; index < first.Length; index++)
        {
            blend[index] = (byte)((first[index] + second[index] + 1) / 2);
        }

        return blend;
    }

    // The mean luma difference in the edge band, inside the foreground, in the background beyond the band, and overall.
    private static (double Edge, double Inside, double Outside, double All) Errors(byte[] frame, byte[] ideal, int shift)
    {
        var sums = new double[3];
        var counts = new long[3];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = ((y * Width) + x) * 4;
                var region = Region(Distance(x, y), shift);
                sums[region] += Math.Abs(Luma(frame, offset) - Luma(ideal, offset));
                counts[region]++;
            }
        }

        return (sums[0] / counts[0], sums[1] / counts[1], sums[2] / counts[2], sums.Sum() / counts.Sum());
    }

    // The edge band reaches as far as the background moves in two frames, at least 32 pixels.
    private static int Region(double distance, int shift)
    {
        if (Math.Abs(distance) <= Math.Max(MinimumBand, 2 * shift))
        {
            return 0;
        }

        if (distance < 0)
        {
            return 1;
        }

        return 2;
    }

    private static double Luma(byte[] pixels, int offset)
    {
        return (pixels[offset] * 0.299) + (pixels[offset + 1] * 0.587) + (pixels[offset + 2] * 0.114);
    }

    // The crops around the foreground: the generated frame, the ideal one, and their difference (gained, on black).
    private static void Save(string name, int shift, byte[] generated, byte[] ideal)
    {
        var folder = Environment.GetEnvironmentVariable("LSFG_ARTIFACT_DIR");

        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var file = $"{name.Replace(' ', '-')}-{shift}px";
        PngFile.Write(Path.Combine(folder, $"{file}-generated.png"), Crop(generated), CropSize, CropSize);
        PngFile.Write(Path.Combine(folder, $"ideal-{shift}px.png"), Crop(ideal), CropSize, CropSize);
        PngFile.Write(Path.Combine(folder, $"{file}-difference.png"), Crop(Difference(generated, ideal)), CropSize, CropSize);
    }

    private static byte[] Difference(byte[] frame, byte[] ideal)
    {
        var difference = new byte[frame.Length];

        for (var offset = 0; offset < frame.Length; offset += 4)
        {
            var gained = Math.Abs(Luma(frame, offset) - Luma(ideal, offset)) * DifferenceGain;
            difference[offset] = (byte)Math.Min(255, gained);
            difference[offset + 1] = (byte)Math.Min(255, gained / 2);
            difference[offset + 3] = 255;
        }

        return difference;
    }

    private static byte[] Crop(byte[] pixels)
    {
        var crop = new byte[CropSize * CropSize * 4];
        var left = CentreX - (CropSize / 2);
        var top = CentreY - (CropSize / 2);

        for (var row = 0; row < CropSize; row++)
        {
            pixels.AsSpan((((top + row) * Width) + left) * 4, CropSize * 4).CopyTo(crop.AsSpan(row * CropSize * 4));
        }

        return crop;
    }
}
