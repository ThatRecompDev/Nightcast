// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Globalization;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Lsfg.Tests.Live;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Media.Windows.Tests;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;
using Xunit.Abstractions;

namespace Phantom.Relay.Desktop.Tests.Pictures;

/// <summary>
/// Whether the decode thread keeps up at x4 (FRUC: x3) with LS1 upscaling to a maximised 4K window: a 1440p H.264 stream fed at
/// the stream's own cadence (60 units a second, every other a repeat, as GTA's 30 fps), each unit decoded and
/// published as the stream does it, with LSFG x4 and NVIDIA FRUC (on the NVIDIA GPU). The thread's time per unit and
/// how far behind the cadence it ends are reported; it keeps up when it does not end a unit behind (no growing queue).
/// FRUC's call holds the thread (about 12, 21 and 30 ms a new picture at x2, x3, x4 here), so x4 asked of it runs x3.
/// </summary>
[Trait("Category", "Live")]
public sealed class QuadrupledDecodeLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int WindowWidth = 3600;
    private const int WindowHeight = 2025;
    private const int Units = 96;
    private const uint RtpPerUnit = 1500;

    private static readonly TimeSpan UnitInterval = TimeSpan.FromSeconds(1.0 / 60);

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public QuadrupledDecodeLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Theory]
    [InlineData(FrameGenerator.Lsfg, 4, 4)]
    [InlineData(FrameGenerator.NvidiaFruc, 4, 3)]
    [InlineData(FrameGenerator.NvidiaFruc, 2, 2)]
    public void The_decode_thread_keeps_up_with_LS1_at_the_multiplier_asked_for_or_FRUCs_most(FrameGenerator generator, int multiplier, int runs)
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Units);
        var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, FirstAdapter.Nvidia(), out var problem);
        Assert.True(decoder is not null, problem);
        using var shared = new SharedPictureDecoder(decoder, TimeProvider.System, _log, new ShownPictureTap());
        shared.Offer(FirstAdapter.Nvidia());

        shared.Scale(new PictureScale(UpscaleMethod.Ls1, WindowWidth, WindowHeight));
        shared.Use(FrameGeneration.QualityDoubled with { Multiplier = multiplier, Generator = generator });

        var (times, behind) = Play(shared, units);
        _output.WriteLine($"report: {shared.Report}");
        Report($"{Name(generator)} x{multiplier} asked, LS1", times, behind);

        Assert.StartsWith($"{Name(generator)} x{runs}", shared.Report.FrameGeneration, StringComparison.Ordinal);
        Assert.True(behind[^1] < UnitInterval.TotalMilliseconds, $"the decode thread ended {behind[^1]:F1} ms behind the stream");
    }
    // The units at the stream's cadence; each unit's time on the decode thread, and how far behind its due time it ended.
    private static (List<double> Times, List<double> Behind) Play(SharedPictureDecoder shared, List<byte[]> units)
    {
        var (times, behind) = (new List<double>(), new List<double>());
        var started = Stopwatch.GetTimestamp();

        for (var index = 0; index < units.Count; index++)
        {
            Wait(started, UnitInterval * index);
            var unit = Stopwatch.GetTimestamp();
            shared.Prepare();
            Assert.Equal(VideoDecodeResult.Picture, shared.Decode(units[index], [], index * SyntheticH264Encoding.FrameUnits * 100));
            var kind = index % 2 == 1 ? PictureKind.Repeat : PictureKind.New;
            _ = shared.Publish(kind, index, (uint)index * RtpPerUnit, out _, out _);

            while (shared.TryTake(out var picture))
            {
                shared.Release(picture, 0, 0);
            }

            times.Add(Stopwatch.GetElapsedTime(unit).TotalMilliseconds);
            behind.Add(Math.Max(0, (Stopwatch.GetElapsedTime(started) - (UnitInterval * (index + 1))).TotalMilliseconds));
        }

        return (times, behind);
    }

    private static void Wait(long started, TimeSpan due)
    {
        while (Stopwatch.GetElapsedTime(started) < due)
        {
            Thread.SpinWait(1);
        }
    }

    private static string Name(FrameGenerator generator)
    {
        if (generator == FrameGenerator.NvidiaFruc)
        {
            return "NVIDIA FRUC";
        }

        return "LSFG";
    }

    // The first second's units are warm-up (the stage is made at the first picture); the rest are reported.
    private void Report(string name, List<double> times, List<double> behind)
    {
        var steady = times.Skip(Units / 2).ToList();
        var newOnes = steady.Where((_, index) => index % 2 == 0).ToList();
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{name}: decode thread per new unit {newOnes.Average():F2} ms (worst {newOnes.Max():F2}), per repeat {steady.Where((_, index) => index % 2 == 1).Average():F2} ms; behind the cadence at the end {behind[^1]:F2} ms, worst after warm-up {behind.Skip(Units / 2).Max():F2} ms"));
    }
}
