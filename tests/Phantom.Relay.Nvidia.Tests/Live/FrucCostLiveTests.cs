// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Globalization;
using Phantom.Relay.Lsfg;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Lsfg.Tests.Live;
using Xunit.Abstractions;

namespace Phantom.Relay.Nvidia.Tests.Live;

/// <summary>
/// What a new frame's generated frames cost at 2560 x 1440, FRUC beside LSFG, at x2, x3 and x4: the GPU time from the
/// copy in to the device's queue passing FRUC's fence (LSFG: its passes) and the CPU time of the call (FRUC's call
/// holds the thread until its work is done). Frames alternate between two scenes 32 pixels apart. Also FRUC's x4
/// frames against the ideal ones a quarter, a half and three quarters along the pair.
/// </summary>
[Trait("Category", "Live")]
[Collection(FrucOneAtATime.Name)]
public sealed class FrucCostLiveTests
{
    private const int Width = ArtifactScene.Width;
    private const int Height = ArtifactScene.Height;
    private const int WarmUp = 10;
    private const int Measured = 120;
    private const int Shift = 32;
    private const int Quadrupled = 4;

    private static readonly int[] Multipliers = [2, 3, 4];

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public FrucCostLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void A_new_frame_costs_FRUC_and_LSFG_this_much()
    {
        Fruc();
        Lsfg();
    }

    [Fact]
    public void FRUC_makes_x4_from_the_same_pair_at_a_quarter_a_half_and_three_quarters()
    {
        using var device = FrucSdk.NvidiaDevice();
        using var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var problem);
        Assert.True(fruc is not null, problem);
        _ = fruc.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(0)), 0, Quadrupled);
        Assert.Equal(Quadrupled - 1, fruc.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(Shift)), 0, Quadrupled));

        for (var index = 0; index < Quadrupled - 1; index++)
        {
            var frame = device.ReadRgba(fruc.Output(index), Width, Height);
            var along = Shift * (index + 1) / Quadrupled;
            var error = ArtifactScene.Errors(frame, ArtifactScene.Scene(along), Shift).All;
            var toMiddle = ArtifactScene.Errors(frame, ArtifactScene.Scene(Shift / 2), Shift).All;
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"frame {index + 1} of 3: error to the ideal frame {along} px along {error:F2}, to the middle frame {toMiddle:F2}"));

            Assert.True(error < 1, $"frame {index + 1} is not the frame {along} px along");
        }

        Assert.Equal(1, fruc.Submit(device.CreateRgba(Width, Height, ArtifactScene.Scene(2 * Shift)), 0, 2));
        Assert.True(ArtifactScene.Errors(device.ReadRgba(fruc.Output(0), Width, Height), ArtifactScene.Ideal(Shift), Shift).All < 1, "the next pair's middle frame is wrong after x4");
    }

    private void Fruc()
    {
        using var device = FrucSdk.NvidiaDevice();
        using var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var problem);
        Assert.True(fruc is not null, problem);
        nint[] scenes = [device.CreateRgba(Width, Height, ArtifactScene.Scene(0)), device.CreateRgba(Width, Height, ArtifactScene.Scene(Shift))];
        using var marks = new GpuMarks(fruc.Device, 2);

        foreach (var multiplier in Multipliers)
        {
            var (gpu, cpu) = Measure(marks, frame => fruc.Submit(scenes[frame % 2], 0, multiplier));
            Report($"NVIDIA FRUC x{multiplier} (RGBA)", gpu, cpu);
        }

        _output.WriteLine($"FRUC gave the previous frame back {fruc.Repeats} times in {fruc.Submitted} frames");
    }

    private void Lsfg()
    {
        var path = LosslessLocator.Find(Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL"), LosslessLocator.SteamLibraries());
        Assert.True(path is not null, "Lossless.dll was not found");
        using var catalog = LsfgShaderCatalog.TryLoad(path, _log, out var problem);
        Assert.True(catalog is not null, problem);

        foreach (var multiplier in Multipliers)
        {
            // A pipeline built for the multiplier, as the stream builds it.
            using var device = FrucSdk.NvidiaDevice();
            nint[] scenes = [device.CreateRgba(Width, Height, ArtifactScene.Scene(0)), device.CreateRgba(Width, Height, ArtifactScene.Scene(Shift))];
            using var lsfg = LsfgGenerator.TryCreate(device.Device, catalog, LsfgSettings.Default with { Enabled = true, Multiplier = multiplier }, Width, Height, LsfgFrameFormat.Rgba8, _log, out problem);
            Assert.True(lsfg is not null, problem);

            using var marks = new GpuMarks(lsfg.Device, 2);
            var outputs = new nint[Quadrupled - 1];

            var (gpu, cpu) = Measure(marks, frame =>
            {
                lsfg.Submit(scenes[frame % 2]);

                return lsfg.Generate(multiplier, outputs);
            });
            Report($"LSFG x{multiplier} quality", gpu, cpu);
        }
    }

    // The GPU time of each new frame's work (read back by waiting, so frames do not overlap) and the CPU time of the call.
    private static (List<double> Gpu, List<double> Cpu) Measure(GpuMarks marks, Func<int, int> submit)
    {
        var (gpu, cpu) = (new List<double>(), new List<double>());
        var times = new double[2];

        for (var frame = 0; frame < WarmUp + Measured; frame++)
        {
            marks.Begin();
            var started = Stopwatch.GetTimestamp();
            _ = submit(frame);
            var called = Stopwatch.GetElapsedTime(started);
            marks.Mark(1);
            marks.End();

            if (marks.Read(times) && frame >= WarmUp)
            {
                gpu.Add(times[1]);
                cpu.Add(called.TotalMilliseconds);
            }
        }

        return (gpu, cpu);
    }

    private void Report(string name, List<double> gpu, List<double> cpu)
    {
        gpu.Sort();
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{name,-24} | per new frame: GPU {gpu.Average(),5:F2} ms (median {gpu[gpu.Count / 2]:F2}, worst {gpu[^1]:F2}) | CPU in the call {cpu.Average(),6:F3} ms | {gpu.Count} frames"));
    }
}
