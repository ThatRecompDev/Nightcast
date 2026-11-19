// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Graphics.Sokol;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Lsfg.Tests.Live;
using Xunit.Abstractions;
using static Phantom.Relay.Lsfg.Tests.Live.UpscaleTestImages;

namespace Phantom.Relay.App.Tests.Pictures;

/// <summary>
/// The engine's side of the upscaler comparison, on the test pictures the Lossless upscalers' live test uses
/// (<see cref="UpscaleTestImages"/>): bilinear and FSR 1 at F7's sharpness steps, from 1440p to 2160p through the
/// engine's own window and device, each compared with the 2160p truth (luma PSNR, edge contrast against the truth's).
/// With LSFG_UPSCALE_DIR set it writes the same crops as the Lossless test, for side-by-side viewing.
/// </summary>
[Trait("Category", "Live")]
public sealed class UpscaleComparisonLiveTests
{
    private const int Border = 16;

    private readonly ITestOutputHelper _output;

    public UpscaleComparisonLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Bilinear_and_fsr_at_each_sharpness_against_the_truth()
    {
        var cases = (from scene in new[] { Scene.Chart, Scene.Natural } from variant in new[] { Variant.Clean, Variant.Soft, Variant.Blocky } select (scene, variant)).ToList();
        var pictures = cases.Select(static item => Picture(item.scene, item.variant)).ToList();
        var bench = Run(pictures);
        var directory = Environment.GetEnvironmentVariable("LSFG_UPSCALE_DIR");
        Assert.True(bench.Problem is null, bench.Problem);

        for (var index = 0; index < cases.Count; index++)
        {
            var (scene, variant) = cases[index];
            var truth = Render(scene, TruthWidth, TruthHeight);
            var bilinearGradient = 0.0;

            for (var way = 0; way < UpscaleComparisonBench.Sharpnesses.Length; way++)
            {
                var result = bench.Results[(index * UpscaleComparisonBench.Sharpnesses.Length) + way];
                Assert.True(result is not null, $"{scene} {variant} way {way} never came back");
                var rgba = ToRgba(result);
                var name = Name(UpscaleComparisonBench.Sharpnesses[way]);
                var (psnr, contrast) = (LumaPsnr(rgba, truth, TruthWidth, TruthHeight, Border), Gradient(rgba, TruthWidth, TruthHeight, Border) / Gradient(truth, TruthWidth, TruthHeight, Border));
                _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{scene} {variant}: {name} PSNR {psnr:F2} dB, edge contrast {contrast:F3} of the truth's"));

                Save(directory, scene, variant, name, rgba);

                if (way == 0)
                {
                    bilinearGradient = contrast;
                }
                else
                {
                    Assert.True(contrast > bilinearGradient, $"{name} is no sharper than bilinear");
                }
            }
        }

        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"FSR 1's passes (picture, EASU, RCAS) average {bench.FsrMilliseconds / Math.Max(1, bench.FsrFrames):F3} ms over {bench.FsrFrames} timed frames"));
    }

    private static string Name(float? sharpness)
    {
        if (sharpness is null)
        {
            return "engine-bilinear";
        }

        return string.Create(CultureInfo.InvariantCulture, $"FSR-{sharpness.Value:F1}");
    }

    private static byte[] ToRgba(byte[] bgra)
    {
        var rgba = new byte[bgra.Length];

        for (var index = 0; index < bgra.Length; index += 4)
        {
            rgba[index] = bgra[index + 2];
            rgba[index + 1] = bgra[index + 1];
            rgba[index + 2] = bgra[index];
            rgba[index + 3] = 255;
        }

        return rgba;
    }

    private static void Save(string? directory, Scene scene, Variant variant, string name, byte[] rgba)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var (x, y) = scene == Scene.Chart ? (2560, 880) : (1500, 900);
        var crop = Crop(rgba, TruthWidth, x, y, 640, 400);
        PngFile.Write(Path.Combine(directory, $"{scene}-{variant}-{name}.png"), crop, 640, 400);
    }

    private static UpscaleComparisonBench Run(List<byte[]> pictures)
    {
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var application = new UpscaleComparisonBench(device, window, pictures);
        var driver = new ApplicationDriver(application, new InputFrame(InputLimits.Default), new NoGamepads());

        SokolRunner.Run(driver, new SokolWindow(640, 360, "Relay upscaler comparison") { UnpacedHiddenFrames = true }, SokolLimits.Default);

        return application;
    }
}
