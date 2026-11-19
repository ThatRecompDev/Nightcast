// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Globalization;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Lsfg.Upscaling;
using Phantom.Relay.Media.Windows.Interop;
using Xunit.Abstractions;
using static Phantom.Relay.Lsfg.Tests.Live.UpscaleTestImages;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// Lossless Scaling's upscalers on this machine's GPU with the user's own Lossless.dll: each test picture (a chart and
/// a natural texture, clean, softened and blocky; <see cref="UpscaleTestImages"/>) goes the stream's way (NV12, the
/// video processor to RGBA) and is upscaled from 1440p to 2160p by every upscaler and network, then compared with the
/// 2160p truth (luma PSNR, and edge contrast against the truth's) beside a plain bilinear resize of the same RGBA.
/// With LSFG_UPSCALE_DIR set it writes side-by-side crops as PNG. Also measures the shaders' open choices and each
/// upscaler's GPU time.
/// </summary>
[Trait("Category", "Live")]
public sealed class LosslessUpscalerLiveTests
{
    private const int Border = 16;
    private const int Timed = 40;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public LosslessUpscalerLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    public static TheoryData<LosslessUpscalerKind, int> Networks()
    {
        var data = new TheoryData<LosslessUpscalerKind, int>();

        for (var level = LosslessUpscalerSettings.MinimumLevel; level <= LosslessUpscalerSettings.MaximumLevel; level++)
        {
            data.Add(LosslessUpscalerKind.Ls1, level);
            data.Add(LosslessUpscalerKind.Ls1Performance, level);
        }

        data.Add(LosslessUpscalerKind.Sgsr, 0);

        return data;
    }

    [Theory]
    [MemberData(nameof(Networks))]
    public void Every_upscaling_shader_has_its_shape(LosslessUpscalerKind kind, int level)
    {
        using var catalog = OpenCatalog();
        using var device = new D3D11TestDevice();
        var settings = LosslessUpscalerSettings.Default with { Kind = kind, Level = level };

        using var upscaler = LosslessUpscaler.TryCreate(device.Device, catalog, settings, LosslessUpscaleGeometry.Exact(PictureWidth, PictureHeight, TruthWidth, TruthHeight), _log, out var problem);

        Assert.True(upscaler is not null, problem);
    }

    /// <summary>The network's read origin and the features' format, each way, on the clean chart: the best is wired.</summary>
    [Fact]
    public void The_open_choices_measured()
    {
        using var catalog = OpenCatalog();
        using var device = new D3D11TestDevice();
        var truth = Render(Scene.Chart, TruthWidth, TruthHeight);
        var (input, inputPixels) = Convert(device, Picture(Scene.Chart, Variant.Clean));
        _output.WriteLine($"bilinear: {Score(Bilinear(inputPixels, PictureWidth, PictureHeight, TruthWidth, TruthHeight), truth)}");

        foreach (var kind in new[] { LosslessUpscalerKind.Ls1, LosslessUpscalerKind.Ls1Performance })
        {
            foreach (var shift in new[] { -1, 0, 1 })
            {
                foreach (var format in new[] { Direct3D11Values.Rgba8Format, Direct3D11Values.Rgba16FloatFormat })
                {
                    var wiring = new LosslessUpscaleWiring(shift, format);
                    var settings = LosslessUpscalerSettings.Default with { Kind = kind };
                    var output = Upscale(device, catalog, settings, wiring, input, LosslessUpscaleGeometry.Exact(PictureWidth, PictureHeight, TruthWidth, TruthHeight));
                    var name = format == Direct3D11Values.Rgba8Format ? "rgba8" : "rgba16f";
                    _output.WriteLine($"{kind} shift {shift} features {name}: {Score(output, truth)}");
                }
            }
        }
    }

    [Theory]
    [InlineData(Scene.Chart)]
    [InlineData(Scene.Natural)]
    public void The_upscalers_against_bilinear_and_the_truth(Scene scene)
    {
        using var catalog = OpenCatalog();
        using var device = new D3D11TestDevice();
        var truth = Render(scene, TruthWidth, TruthHeight);
        var directory = Environment.GetEnvironmentVariable("LSFG_UPSCALE_DIR");

        foreach (var variant in new[] { Variant.Clean, Variant.Soft, Variant.Blocky })
        {
            var (input, inputPixels) = Convert(device, Picture(scene, variant));
            var bilinear = Bilinear(inputPixels, PictureWidth, PictureHeight, TruthWidth, TruthHeight);
            var bilinearPsnr = LumaPsnr(bilinear, truth, TruthWidth, TruthHeight, Border);
            var bilinearGradient = Gradient(bilinear, TruthWidth, TruthHeight, Border);
            var crops = new List<(string Name, byte[] Pixels)> { ("truth", truth), ("bilinear", bilinear) };
            _output.WriteLine($"{scene} {variant}: bilinear {Score(bilinear, truth)}");

            foreach (var (kind, level) in Methods())
            {
                var settings = LosslessUpscalerSettings.Default with { Kind = kind, Level = level };
                var output = Upscale(device, catalog, settings, LosslessUpscaleWiring.Standard, input, LosslessUpscaleGeometry.Exact(PictureWidth, PictureHeight, TruthWidth, TruthHeight));
                _output.WriteLine($"{scene} {variant}: {Name(kind, level)} {Score(output, truth)}");
                crops.Add((Name(kind, level), output));

                // Every upscaler adds edge contrast over bilinear and stays a plausible picture of the scene.
                Assert.True(Gradient(output, TruthWidth, TruthHeight, Border) > bilinearGradient, $"{Name(kind, level)} is no sharper than bilinear");
                Assert.True(LumaPsnr(output, truth, TruthWidth, TruthHeight, Border) > bilinearPsnr - 3, $"{Name(kind, level)} is far from the truth");
            }

            Save(directory, scene, variant, crops);
        }
    }

    /// <summary>
    /// The decoder's path: an NV12 picture in, LS1 to a maximised 4K window's letterbox (3600 x 2025, in an NV12 slot
    /// of 3600 x 2026), NV12 out; its luma against the truth's, beside the luma of a bilinear resize.
    /// </summary>
    [Fact]
    public void The_stage_writes_the_upscaled_picture_into_an_nv12_slot()
    {
        const int Width = 3600;
        const int Height = 2025;
        const int SlotHeight = 2026;
        using var device = new D3D11TestDevice();
        var picture = Picture(Scene.Chart, Variant.Clean);
        var truth = Render(Scene.Chart, Width, Height);

        var nv12 = device.CreateNv12(PictureWidth, PictureHeight, ToNv12(picture, PictureWidth, PictureHeight));
        var slot = device.CreateNv12Target(Width, SlotHeight);
        var geometry = new LosslessUpscaleGeometry(PictureWidth, PictureHeight, Width, Height, Width, SlotHeight);

        using var stage = LosslessUpscaleStage.TryCreate(device.Device, LosslessUpscalerSettings.Default, geometry, _log, out var problem);
        Assert.True(stage is not null, problem);
        stage.Begin();
        Assert.True(stage.Write(nv12, 0, slot));
        stage.Done();

        var luma = device.ReadLuma(slot, Width, SlotHeight).AsSpan(0, Width * Height).ToArray();
        var truthLuma = ToYuv420(truth, Width, Height).Luma;
        var bilinearLuma = ToYuv420(Bilinear(picture, PictureWidth, PictureHeight, Width, Height), Width, Height).Luma;
        var (upscaled, bilinear) = (PlanePsnr(luma, truthLuma, Width, Height), PlanePsnr(bilinearLuma, truthLuma, Width, Height));
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"luma PSNR against the truth: LS1 through NV12 {upscaled:F2} dB, bilinear {bilinear:F2} dB"));
        Assert.True(upscaled > bilinear, "the upscaled NV12 picture is no closer to the truth than a bilinear resize");
    }

    [Theory]
    [InlineData(TruthWidth, TruthHeight)]
    [InlineData(3600, 2025)]
    public void The_upscalers_cost(int width, int height)
    {
        using var catalog = OpenCatalog();
        using var device = new D3D11TestDevice();
        var (input, _) = Convert(device, Picture(Scene.Natural, Variant.Clean));
        var geometry = new LosslessUpscaleGeometry(PictureWidth, PictureHeight, width, height, (width + 1) & ~1, (height + 1) & ~1);

        foreach (var (kind, level) in new[] { (LosslessUpscalerKind.Ls1, LosslessUpscalerSettings.DefaultLevel), (LosslessUpscalerKind.Ls1Performance, LosslessUpscalerSettings.DefaultLevel), (LosslessUpscalerKind.Sgsr, 0) })
        {
            var settings = LosslessUpscalerSettings.Default with { Kind = kind, Level = level };
            using var upscaler = LosslessUpscaler.TryCreate(device.Device, catalog, settings, geometry, _log, out var problem);
            Assert.True(upscaler is not null, problem);
            var times = Time(upscaler, input);
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{Name(kind, level)} {PictureWidth}x{PictureHeight} to {width}x{height}: {upscaler.Passes} passes, GPU average {times.Average():F3} ms, median {Median(times):F3} ms, worst {times.Max():F3} ms"));
        }
    }

    private static IEnumerable<(LosslessUpscalerKind Kind, int Level)> Methods()
    {
        for (var level = LosslessUpscalerSettings.MinimumLevel; level <= LosslessUpscalerSettings.MaximumLevel; level++)
        {
            yield return (LosslessUpscalerKind.Ls1, level);
        }

        foreach (var level in new[] { LosslessUpscalerSettings.MinimumLevel, LosslessUpscalerSettings.DefaultLevel, LosslessUpscalerSettings.MaximumLevel })
        {
            yield return (LosslessUpscalerKind.Ls1Performance, level);
        }

        yield return (LosslessUpscalerKind.Sgsr, 0);
    }

    private static string Name(LosslessUpscalerKind kind, int level)
    {
        return kind switch
        {
            LosslessUpscalerKind.Ls1 => $"LS1-{level}",
            LosslessUpscalerKind.Ls1Performance => $"LS1P-{level}",
            _ => "SGSR",
        };
    }

    private static string Score(byte[] image, byte[] truth)
    {
        var psnr = LumaPsnr(image, truth, TruthWidth, TruthHeight, Border);
        var contrast = Gradient(image, TruthWidth, TruthHeight, Border) / Gradient(truth, TruthWidth, TruthHeight, Border);

        return string.Create(CultureInfo.InvariantCulture, $"PSNR {psnr:F2} dB, edge contrast {contrast:F3} of the truth's");
    }

    private static double PlanePsnr(byte[] plane, byte[] truth, int width, int height)
    {
        double sum = 0;
        long count = 0;

        for (var y = Border; y < height - Border; y++)
        {
            for (var x = Border; x < width - Border; x++)
            {
                var difference = plane[(y * width) + x] - (double)truth[(y * width) + x];
                sum += difference * difference;
                count++;
            }
        }

        return 10 * Math.Log10(255.0 * 255.0 / Math.Max(sum / count, 1e-9));
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();

        return sorted[sorted.Count / 2];
    }

    // The picture the stream's way: NV12 on the GPU, the video processor to RGBA; that RGBA read back too.
    private static (nint Texture, byte[] Pixels) Convert(D3D11TestDevice device, byte[] rgba)
    {
        var nv12 = device.CreateNv12(PictureWidth, PictureHeight, ToNv12(rgba, PictureWidth, PictureHeight));
        var converter = Nv12ToRgbaConverter.TryCreate(device.Device, PictureWidth, PictureHeight, out var problem);
        Assert.True(converter is not null, problem);
        Assert.True(converter.Convert(nv12, 0));

        // A copy the device owns, so the converter may go.
        var copy = device.CreateRgba(PictureWidth, PictureHeight, new byte[PictureWidth * PictureHeight * 4]);
        device.Copy(copy, converter.Output);
        converter.Dispose();

        return (copy, device.ReadRgba(copy, PictureWidth, PictureHeight));
    }

    private static void Save(string? directory, Scene scene, Variant variant, List<(string Name, byte[] Pixels)> images)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var (x, y) = scene == Scene.Chart ? (2560, 880) : (1500, 900);
        const int CropWidth = 640;
        const int CropHeight = 400;
        var tiles = new List<byte[]>();

        foreach (var (name, pixels) in images)
        {
            var crop = Crop(pixels, TruthWidth, x, y, CropWidth, CropHeight);
            tiles.Add(crop);
            PngFile.Write(Path.Combine(directory, $"{scene}-{variant}-{name}.png"), crop, CropWidth, CropHeight);
        }

        var (montage, width, height) = Montage(tiles, CropWidth, CropHeight, 3);
        PngFile.Write(Path.Combine(directory, $"{scene}-{variant}-all.png"), montage, width, height);
    }

    private byte[] Upscale(D3D11TestDevice device, LsfgShaderCatalog catalog, LosslessUpscalerSettings settings, LosslessUpscaleWiring wiring, nint input, LosslessUpscaleGeometry geometry)
    {
        using var upscaler = LosslessUpscaler.TryCreate(device.Device, catalog, settings, geometry, wiring, _log, out var problem);
        Assert.True(upscaler is not null, problem);
        Assert.True(upscaler.Upscale(input));

        return device.ReadRgba(upscaler.Output, geometry.OutputTextureWidth, geometry.OutputTextureHeight);
    }

    private static List<double> Time(LosslessUpscaler upscaler, nint input)
    {
        using var timer = new GpuTimer(upscaler.Device);
        var times = new List<double>();

        for (var run = 0; run < Timed + 5; run++)
        {
            timer.Begin();
            upscaler.Upscale(input);
            timer.End();
            var clock = Stopwatch.StartNew();
            double milliseconds;

            while (!timer.TryRead(out milliseconds))
            {
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), "the GPU never reached the end of the timed work");
                Thread.Yield();
            }

            if (run >= 5)
            {
                times.Add(milliseconds);
            }
        }

        return times;
    }

    private LsfgShaderCatalog OpenCatalog()
    {
        var path = Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL");
        var found = LosslessLocator.Find(path, LosslessLocator.SteamLibraries());
        Assert.True(found is not null, "Lossless.dll was not found in the Steam libraries; set LSFG_LOSSLESS_DLL");
        var catalog = LsfgShaderCatalog.TryLoad(found, _log, out var problem);
        Assert.True(catalog is not null, problem);

        return catalog;
    }
}
