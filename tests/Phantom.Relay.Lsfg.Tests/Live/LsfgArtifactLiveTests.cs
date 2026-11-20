// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Xunit.Abstractions;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// The third-person case (<see cref="ArtifactScene"/>): three frames go in (the background moved 0, S and 2S pixels)
/// and the frame between the last two comes out; it is compared with the ideal middle frame (the background moved
/// 1.5 S, the foreground where it is) in the edge band, the foreground and the background. Each of the graph's
/// unsettled choices (<see cref="LsfgWiring"/>) is run. With LSFG_ARTIFACT_DIR set, the crops around the foreground
/// (generated, ideal, and the difference) are written there as PNG.
/// </summary>
[Trait("Category", "Live")]
public sealed class LsfgArtifactLiveTests
{
    private const int Width = ArtifactScene.Width;
    private const int Height = ArtifactScene.Height;
    private const int CropSize = ArtifactScene.CropSize;

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
        using var device = new D3D11TestDevice(0);

        var wiring = new LsfgWiring(betaNewestFirst, maskShift, pointSampling, withoutMask);
        var settings = LsfgSettings.Default with { Enabled = true };
        using var generator = LsfgGenerator.TryCreate(device.Device, catalog, settings, Width, Height, LsfgFrameFormat.Rgba8, wiring, _log, out problem);
        Assert.True(generator is not null, problem);

        foreach (var shift in ArtifactScene.Shifts)
        {
            var generated = Generate(device, generator, shift);
            var ideal = ArtifactScene.Ideal(shift);
            var errors = ArtifactScene.Errors(generated, ideal, shift);
            var reference = ArtifactScene.Errors(ArtifactScene.Blend(ArtifactScene.Scene(shift), ArtifactScene.Scene(2 * shift)), ideal, shift);
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
        generator.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(0)));
        generator.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(shift)));
        generator.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(2 * shift)));
        Assert.Equal(1, generator.Generate(2, outputs));

        return device.ReadRgba(outputs[0], Width, Height);
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
        PngFile.Write(Path.Combine(folder, $"{file}-generated.png"), ArtifactScene.Crop(generated), CropSize, CropSize);
        PngFile.Write(Path.Combine(folder, $"ideal-{shift}px.png"), ArtifactScene.Crop(ideal), CropSize, CropSize);
        PngFile.Write(Path.Combine(folder, $"{file}-difference.png"), ArtifactScene.Crop(ArtifactScene.Difference(generated, ideal)), CropSize, CropSize);
    }
}
