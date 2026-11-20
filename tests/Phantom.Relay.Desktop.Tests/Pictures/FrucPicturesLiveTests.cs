// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Lsfg.Tests.Live;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Media.Windows.Tests;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;
using Xunit.Abstractions;

namespace Phantom.Relay.Desktop.Tests.Pictures;

/// <summary>
/// NVIDIA FRUC on the decoder's GPU path on this machine (its NVIDIA GPU, the owner's SDK): a 1440p H.264 stream as a
/// 30 fps game sends it, decoded on the NVIDIA GPU and published, with F4's cycle played through: LSFG, FRUC at x2 and
/// x4 (which runs x3, the most FRUC keeps up with), off, then FRUC again (the decoder's one FRUC, paused and resumed). Generated pictures are handed over and the
/// stats say which generator runs.
/// </summary>
[Trait("Category", "Live")]
public sealed class FrucPicturesLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int Units = 12;
    private const uint RtpPerUnit = 1500;

    private static readonly FrameGeneration Fruc = FrameGeneration.QualityDoubled with { Generator = FrameGenerator.NvidiaFruc };

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public FrucPicturesLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void FRUC_and_LSFG_take_turns_on_the_decoders_GPU_path()
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Units);
        var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, FirstAdapter.Nvidia(), out var problem);
        Assert.True(decoder is not null, problem);
        using var shared = new SharedPictureDecoder(decoder, TimeProvider.System, _log);
        shared.Offer(FirstAdapter.Nvidia());

        Assert.True(Generated(shared, units, FrameGeneration.QualityDoubled, "LSFG x2"));
        Assert.True(Generated(shared, units, Fruc, "NVIDIA FRUC x2"));
        Assert.True(Generated(shared, units, Fruc with { Multiplier = 4 }, "NVIDIA FRUC x3 → 90 fps, x4 capped"));

        Assert.False(Generated(shared, units, FrameGeneration.Off, GpuPictureReport.GenerationOff));
        Assert.True(Generated(shared, units, Fruc, "NVIDIA FRUC x2"));
    }

    // The stream played with these settings; whether generated pictures were handed over (the pacer hands over the slot
    // due at each take, so how many a group held is not seen here).
    private bool Generated(SharedPictureDecoder shared, List<byte[]> units, FrameGeneration settings, string report)
    {
        shared.Use(settings);
        var generated = false;

        for (var index = 0; index < units.Count; index++)
        {
            shared.Prepare();
            Assert.Equal(VideoDecodeResult.Picture, shared.Decode(units[index], [], index * SyntheticH264Encoding.FrameUnits * 100));
            _ = shared.Publish(PictureKind.New, index, (uint)index * RtpPerUnit, out _, out _);

            while (shared.TryTake(out var picture))
            {
                generated |= picture.Generated;
                shared.Release(picture, 0, 0);
            }
        }

        _output.WriteLine($"{settings.Generator} x{settings.ClampedMultiplier} {settings.Mode}: {shared.Report.FrameGeneration}");
        Assert.True(shared.IsSharing, shared.Report.Path);
        Assert.StartsWith(report, shared.Report.FrameGeneration, StringComparison.Ordinal);

        return generated;
    }
}
