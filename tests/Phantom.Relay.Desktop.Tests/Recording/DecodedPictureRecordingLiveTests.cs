// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Desktop.Tests.Pictures;
using Phantom.Relay.Lsfg.Tests.Live;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Media.Windows.Tests;
using Phantom.Relay.Media.Windows.Tests.Recording;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media;
using Xunit.Abstractions;

namespace Phantom.Relay.Desktop.Tests.Recording;

/// <summary>
/// The recorder on the decoder's real GPU path: a synthetic H.264 stream decoded by the hardware decoder and published
/// into its shared ring as the stream does, each picture the "render thread" takes handed through the decoder's tap
/// (<see cref="ShownPictureTap"/>) to the recorder, then shown.mp4 read back: every picture taken once the encoder was
/// ready is in it, at the stream's size.
/// </summary>
[Trait("Category", "Live")]
public sealed class DecodedPictureRecordingLiveTests
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int Units = 60;
    private const uint RtpPerUnit = 1500;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public DecodedPictureRecordingLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void Each_picture_taken_from_the_decoders_ring_is_in_shown_mp4()
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Units);
        var folder = Path.GetDirectoryName(RecordingTestFolder.File(nameof(DecodedPictureRecordingLiveTests), RecordingFolder.SourceFile))!;
        var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, out var problem);
        Assert.True(decoder is not null, problem);
        var tap = new ShownPictureTap();
        using var shared = new SharedPictureDecoder(decoder, TimeProvider.System, _log, tap);

        shared.Offer(FirstAdapter.Luid());
        shared.Use(FrameGeneration.Off);

        var source = new SourceRecorder(Path.Combine(folder, RecordingFolder.SourceFile), Width, Height, new AccessUnitTap(), TimeProvider.System, _log);
        var shown = new ShownRecorder(Path.Combine(folder, RecordingFolder.ShownFile), CompareLabels.ConsoleRate, source, TimeProvider.System, _log);
        source.Start();
        shown.Start();
        tap.Attach(shown);

        var taken = Play(shared, source, shown, units);
        tap.Detach();
        var outcome = shown.Finish();
        _ = source.Finish();

        _output.WriteLine($"taken once ready {taken}; {RecordingReport.Describe(outcome.Facts, outcome.Problem)}; {outcome}");
        Assert.True(tap.HasRing);
        Assert.Null(outcome.Problem);
        Assert.Equal(taken, outcome.Written + outcome.Busy + outcome.Dropped);
        Assert.Equal((outcome.Written, Width, Height), (outcome.Facts!.Frames, outcome.Facts.Width, outcome.Facts.Height));
    }

    // Each unit decoded, tapped and published as the stream does; each picture taken and released as the renderer does;
    // after the first, a wait for the encoder. The pictures taken once it was ready.
    private static int Play(SharedPictureDecoder shared, SourceRecorder source, ShownRecorder shown, List<byte[]> units)
    {
        var taken = 0;

        for (var index = 0; index < units.Count; index++)
        {
            shared.Prepare();
            source.Received(units[index], (uint)index * RtpPerUnit, AnnexBUnits.IsKeyFrame(units[index]));
            Assert.Equal(VideoDecodeResult.Picture, shared.Decode(units[index], [], index * SyntheticH264Encoding.FrameUnits * 100));
            _ = shared.Publish(PictureKind.New, index, (uint)index * RtpPerUnit, out _, out _);
            var ready = shown.IsReady;

            while (shared.TryTake(out var picture))
            {
                if (ready)
                {
                    taken++;
                }

                shared.Release(picture, 0, 0);
            }

            WaitForEncoder(shown);
            Thread.Sleep(1000 / CompareLabels.ConsoleRate);
        }

        return taken;
    }

    private static void WaitForEncoder(ShownRecorder shown)
    {
        var waited = Stopwatch.StartNew();

        while (!shown.IsReady && waited.Elapsed < TimeSpan.FromSeconds(5))
        {
            Thread.Sleep(10);
        }
    }
}
