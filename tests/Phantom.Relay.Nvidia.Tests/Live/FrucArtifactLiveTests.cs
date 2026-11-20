// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Lsfg;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Lsfg.Tests.Live;
using Xunit.Abstractions;

namespace Phantom.Relay.Nvidia.Tests.Live;

/// <summary>
/// NVIDIA FRUC and LSFG side by side on LSFG's third-person case (<see cref="ArtifactScene"/>): three frames go in
/// (the background moved 0, S and 2S pixels), the frame between the last two comes out, and each is scored against
/// the ideal middle frame in the edge band, the foreground, the background and the whole frame. FRUC runs on RGBA
/// (as the stream gives it the video processor's RGBA) and on NV12 (the decoder's own format; the scene goes through
/// NV12 (made on the CPU, as the decoder gives it) and back to RGBA on the video processor, whose own loss is given as the NV12 floor), each on a device of its own (one
/// FRUC per device). With FRUC_ARTIFACT_DIR set, the crops (FRUC | LSFG | ideal, and the differences of FRUC and LSFG)
/// are written there as PNG.
/// </summary>
[Trait("Category", "Live")]
[Collection(FrucOneAtATime.Name)]
public sealed class FrucArtifactLiveTests
{
    private const int Width = ArtifactScene.Width;
    private const int Height = ArtifactScene.Height;
    private const int CropSize = ArtifactScene.CropSize;
    private const int Panels = 3;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public FrucArtifactLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void FRUC_and_LSFG_around_a_still_foreground_over_a_moving_background()
    {
        var fruc = FrucRgba();
        var frucNv12 = FrucNv12(out var floors);
        var lsfg = Lsfg();

        foreach (var shift in ArtifactScene.Shifts)
        {
            var ideal = ArtifactScene.Ideal(shift);
            var blend = ArtifactScene.Blend(ArtifactScene.Scene(shift), ArtifactScene.Scene(2 * shift));

            Report(shift, "NVIDIA FRUC (RGBA)", fruc[shift], ideal);
            Report(shift, "NVIDIA FRUC (NV12)", frucNv12[shift], ideal);
            Report(shift, "NV12 round trip of the ideal (floor)", floors[shift], ideal);
            Report(shift, "LSFG x2 quality", lsfg[shift], ideal);
            Report(shift, "plain blend", blend, ideal);

            Save(shift, fruc[shift], lsfg[shift], ideal);
            SaveNv12(shift, frucNv12[shift], floors[shift], ideal);

            Assert.True(ArtifactScene.Errors(fruc[shift], ideal, shift).All < ArtifactScene.Errors(blend, ideal, shift).All, "FRUC's frame is no closer to the ideal than a blend");
        }
    }

    private Dictionary<int, byte[]> FrucRgba()
    {
        var frames = new Dictionary<int, byte[]>();
        using var device = FrucSdk.NvidiaDevice();
        using var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var problem);
        Assert.True(fruc is not null, problem);

        foreach (var shift in ArtifactScene.Shifts)
        {
            fruc.Restart();
            _ = fruc.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(0)), 0, 2);
            Assert.True(fruc.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(shift)), 0, 2) == 1, fruc.Failure);
            Assert.True(fruc.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(2 * shift)), 0, 2) == 1, fruc.Failure);
            frames[shift] = device.ReadRgba(fruc.Output(0), Width, Height);
        }

        _output.WriteLine($"FRUC (RGBA) gave the previous frame back {fruc.Repeats} of {fruc.Submitted} times");

        return frames;
    }

    // As FrucRgba through NV12: each scene to NV12 on the video processor, FRUC on NV12, its output back to RGBA.
    private Dictionary<int, byte[]> FrucNv12(out Dictionary<int, byte[]> floors)
    {
        var frames = new Dictionary<int, byte[]>();
        floors = [];

        using var device = FrucSdk.NvidiaDevice();
        using var toRgba = Nv12ToRgbaConverter.TryCreate(device.Device, Width, Height, out var problem);
        Assert.True(toRgba is not null, problem);
        using var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Nv12, _log, out problem);
        Assert.True(fruc is not null, problem);

        foreach (var shift in ArtifactScene.Shifts)
        {
            foreach (var moved in new[] { 0, shift, 2 * shift })
            {
                _ = fruc.Submit(ToNv12(device, ArtifactScene.Scene(moved)), 0, 2);
                Assert.Null(fruc.Failure);
            }

            Assert.True(toRgba.Convert(fruc.Output(0), 0));
            frames[shift] = device.ReadRgba(toRgba.Output, Width, Height);

            Assert.True(toRgba.Convert(ToNv12(device, ArtifactScene.Ideal(shift)), 0));
            floors[shift] = device.ReadRgba(toRgba.Output, Width, Height);
        }

        return frames;
    }

    private Dictionary<int, byte[]> Lsfg()
    {
        var frames = new Dictionary<int, byte[]>();
        var path = LosslessLocator.Find(Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL"), LosslessLocator.SteamLibraries());
        Assert.True(path is not null, "Lossless.dll was not found");

        using var device = FrucSdk.NvidiaDevice();
        using var catalog = LsfgShaderCatalog.TryLoad(path, _log, out var problem);
        Assert.True(catalog is not null, problem);
        using var lsfg = LsfgGenerator.TryCreate(device.Device, catalog, LsfgSettings.Default with { Enabled = true }, Width, Height, LsfgFrameFormat.Rgba8, _log, out problem);
        Assert.True(lsfg is not null, problem);
        var outputs = new nint[1];

        foreach (var shift in ArtifactScene.Shifts)
        {
            lsfg.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(0)));
            lsfg.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(shift)));
            lsfg.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(2 * shift)));
            Assert.Equal(1, lsfg.Generate(2, outputs));
            frames[shift] = device.ReadRgba(outputs[0], Width, Height);
        }

        return frames;
    }

    // The frame as the decoder would hand it: BT.709 limited-range NV12, chroma averaged over each 2 x 2 block.
    private static nint ToNv12(D3D11TestDevice device, byte[] frame)
    {
        var planes = new byte[Width * Height * 3 / 2];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var (luma, _, _) = YCbCr(frame, ((y * Width) + x) * 4);
                planes[(y * Width) + x] = (byte)Math.Round(16 + (219 * luma));
            }
        }

        for (var y = 0; y < Height / 2; y++)
        {
            for (var x = 0; x < Width / 2; x++)
            {
                var (blue, red) = Chroma(frame, 2 * x, 2 * y);
                var offset = (Width * Height) + (y * Width) + (2 * x);
                planes[offset] = (byte)Math.Round(128 + (224 * blue));
                planes[offset + 1] = (byte)Math.Round(128 + (224 * red));
            }
        }

        return device.CreateNv12(Width, Height, planes);
    }

    private static (double Blue, double Red) Chroma(byte[] frame, int left, int top)
    {
        double blue = 0;
        double red = 0;

        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            var (_, cb, cr) = YCbCr(frame, ((((top + dy) * Width) + left + dx) * 4));
            blue += cb / 4;
            red += cr / 4;
        }

        return (blue, red);
    }

    private static (double Luma, double Blue, double Red) YCbCr(byte[] frame, int offset)
    {
        var r = frame[offset] / 255.0;
        var g = frame[offset + 1] / 255.0;
        var b = frame[offset + 2] / 255.0;
        var luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);

        return (luma, (b - luma) / 1.8556, (r - luma) / 1.5748);
    }
    private void Report(int shift, string name, byte[] frame, byte[] ideal)
    {
        var errors = ArtifactScene.Errors(frame, ideal, shift);
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{shift,2} px | {name,-38} | edge band {errors.Edge,6:F2} | foreground {errors.Inside,6:F2} | background {errors.Outside,6:F2} | whole frame {errors.All,6:F2}"));
    }

    // FRUC | LSFG | ideal side by side, and the differences of FRUC and LSFG beside a black panel.
    private static void Save(int shift, byte[] fruc, byte[] lsfg, byte[] ideal)
    {
        var folder = Environment.GetEnvironmentVariable("FRUC_ARTIFACT_DIR");

        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var frames = SideBySide(ArtifactScene.Crop(fruc), ArtifactScene.Crop(lsfg), ArtifactScene.Crop(ideal));
        PngFile.Write(Path.Combine(folder, $"fruc-lsfg-ideal-{shift}px.png"), frames, CropSize * Panels, CropSize);

        var black = new byte[CropSize * CropSize * 4];

        for (var alpha = 3; alpha < black.Length; alpha += 4)
        {
            black[alpha] = byte.MaxValue;
        }

        var differences = SideBySide(ArtifactScene.Crop(ArtifactScene.Difference(fruc, ideal)), ArtifactScene.Crop(ArtifactScene.Difference(lsfg, ideal)), black);
        PngFile.Write(Path.Combine(folder, $"fruc-lsfg-difference-{shift}px.png"), differences, CropSize * Panels, CropSize);
    }

    // FRUC on NV12 | the NV12 round trip of the ideal | the ideal.
    private static void SaveNv12(int shift, byte[] fruc, byte[] floor, byte[] ideal)
    {
        var folder = Environment.GetEnvironmentVariable("FRUC_ARTIFACT_DIR");

        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        var frames = SideBySide(ArtifactScene.Crop(fruc), ArtifactScene.Crop(floor), ArtifactScene.Crop(ideal));
        PngFile.Write(Path.Combine(folder, $"fruc-nv12-floor-ideal-{shift}px.png"), frames, CropSize * Panels, CropSize);
    }

    private static byte[] SideBySide(byte[] left, byte[] middle, byte[] right)
    {
        var row = CropSize * 4;
        var joined = new byte[row * Panels * CropSize];
        byte[][] panels = [left, middle, right];

        for (var y = 0; y < CropSize; y++)
        {
            for (var panel = 0; panel < Panels; panel++)
            {
                panels[panel].AsSpan(y * row, row).CopyTo(joined.AsSpan((y * row * Panels) + (panel * row)));
            }
        }

        return joined;
    }
}
