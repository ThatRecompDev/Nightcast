// Copyright (c) 2026 Phantom.Relay. All rights reserved.

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
/// The decoder's GPU path on this machine with the user's own Lossless.dll: a 1440p H.264 stream decoded by the
/// hardware decoder and published as the stream publishes it, with LS1 asked for at a maximised 4K window's letterbox
/// (3600 x 2025), with and without frame generation; the pictures handed over must be at that size in a ring of
/// 3600 x 2026 (NV12's even size), and back at 1440p once the request is withdrawn.
/// </summary>
[Trait("Category", "Live")]
public sealed class SharedPictureDecoderLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int WindowWidth = 3600;
    private const int WindowHeight = 2025;
    private const int Units = 24;
    private const uint RtpPerUnit = 1500;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public SharedPictureDecoderLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ls1_hands_over_pictures_at_the_windows_size_then_at_their_own_again(bool generation)
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Units);
        var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, out var problem);
        Assert.True(decoder is not null, problem);
        Assert.True(decoder.IsHardwareAccelerated, decoder.FallbackReason);

        using var shared = new SharedPictureDecoder(decoder, TimeProvider.System, _log, new ShownPictureTap());
        shared.Offer(FirstAdapter.Luid());
        shared.Scale(new PictureScale(UpscaleMethod.Ls1, WindowWidth, WindowHeight));
        var settings = generation ? FrameGeneration.QualityDoubled : FrameGeneration.Off;
        shared.Use(settings);

        var upscaled = Play(shared, units, 0, Units / 2, generation);
        Assert.True(shared.IsSharing, shared.Report.Path);
        var ring = shared.Ring;
        _output.WriteLine($"report: {shared.Report}");

        Assert.All(upscaled, picture => Assert.Equal((WindowWidth, WindowHeight), (picture.Width, picture.Height)));
        Assert.Equal((WindowWidth, WindowHeight + 1), (ring!.TextureWidth, ring.TextureHeight));
        Assert.StartsWith($"LS1 (network 2) → {WindowWidth}x{WindowHeight}", shared.Report.Upscale);
        Assert.Contains(upscaled, picture => picture.Generated == generation);

        shared.Scale(PictureScale.None);
        var own = Play(shared, units, Units / 2, Units, generation);
        Assert.All(own, picture => Assert.Equal((Width, Height), (picture.Width, picture.Height)));
        Assert.True(shared.Ring!.Generation > ring.Generation);
        Assert.Equal(string.Empty, shared.Report.Upscale);
    }

    // The units from first to last as the stream gives them (every other a repeat with frame generation, as a 30 fps
    // game), each picture handed over taken and released at once; the pictures handed over.
    private static List<GpuPicture> Play(SharedPictureDecoder shared, List<byte[]> units, int first, int last, bool generation)
    {
        var pictures = new List<GpuPicture>();

        for (var index = first; index < last; index++)
        {
            shared.Prepare();
            Assert.Equal(VideoDecodeResult.Picture, shared.Decode(units[index], [], index * SyntheticH264Encoding.FrameUnits * 100));
            var kind = generation && index % 2 == 1 ? PictureKind.Repeat : PictureKind.New;
            _ = shared.Publish(kind, index, (uint)index * RtpPerUnit, out _, out _);

            while (shared.TryTake(out var picture))
            {
                pictures.Add(picture);
                shared.Release(picture, 0, 0);
            }
        }

        Assert.NotEmpty(pictures);

        return pictures;
    }
}
