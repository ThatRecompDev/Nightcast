// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Lsfg.Tests.Live;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Media.Windows.Interop;
using Phantom.Relay.Media.Windows.Recording;
using Phantom.Relay.Media.Windows.Tests;
using Phantom.Relay.Media.Windows.Tests.Recording;
using Phantom.Relay.Stream.Media;
using Xunit.Abstractions;

namespace Phantom.Relay.Desktop.Tests.Recording;

/// <summary>
/// The recorder's two files and the comparison on this machine, from synthetic data on a manual clock: a second of a
/// 60-unit H.264 stream (Windows' encoder, at test time) tapped as the decode thread taps it, and a second of pictures
/// shown at 120 a second (flat NV12 textures on a hardware D3D11 device) as the render thread hands them on; then, where
/// the user has ffmpeg, compare.mp4 and its half-speed twin. Prints what the render thread pays a picture.
/// </summary>
[Trait("Category", "Live")]
public sealed unsafe class RecordingLiveTests
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int Units = 60;
    private const int ShownRate = 120;
    private const uint RtpPerUnit = 1500;
    private const int LumaTolerance = 12;

    private static readonly byte[] Lumas = [40, 90, 140, 190];

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 21, 4, 3, TimeSpan.Zero));

    public RecordingLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void Both_files_hold_what_came_and_what_was_shown_and_ffmpeg_puts_them_side_by_side()
    {
        var folder = Path.GetDirectoryName(RecordingTestFolder.File(nameof(RecordingLiveTests), RecordingFolder.SourceFile))!;
        var units = SyntheticH264Encoding.Encode(Width, Height, Units);
        var wrappers = new StrategyBasedComWrappers();
        using var device = D3D11VideoDevice.TryCreate(wrappers, 0, out var problem);
        Assert.True(device is not null, problem);
        var textures = Lumas.Select(luma => FlatTexture(wrappers, device.Device, luma)).ToArray();

        var source = new SourceRecorder(Path.Combine(folder, RecordingFolder.SourceFile), Width, Height, new AccessUnitTap(), _clock, _log);
        var shown = new ShownRecorder(Path.Combine(folder, RecordingFolder.ShownFile), ShownRate, source, _clock, _log);
        source.Start();
        shown.Start();
        WarmUp(shown, device.Device, textures[0]);

        Play(source, shown, device.Device, units, textures);
        var sourceOutcome = source.Finish();
        var shownOutcome = shown.Finish();
        Release(textures);

        _output.WriteLine($"source: {RecordingReport.Describe(sourceOutcome.Facts, sourceOutcome.Problem)}");
        _output.WriteLine($"shown: {RecordingReport.Describe(shownOutcome.Facts, shownOutcome.Problem)}; {shownOutcome}");
        AssertSource(sourceOutcome);
        AssertShown(shownOutcome, Path.Combine(folder, RecordingFolder.ShownFile));
        Compare(folder, sourceOutcome, shownOutcome);
    }

    // The first picture asks for the encoder; the writer makes it on its own thread.
    private static void WarmUp(ShownRecorder shown, nint device, nint texture)
    {
        var waited = Stopwatch.StartNew();

        while (!shown.IsReady && waited.Elapsed < TimeSpan.FromSeconds(5))
        {
            shown.Shown(device, texture, Width, Height);
            Thread.Sleep(10);
        }

        Assert.True(shown.IsReady);
    }

    // A second: a unit every other refresh at 120, as the decode thread taps them, and a picture shown every refresh.
    private void Play(SourceRecorder source, ShownRecorder shown, nint device, List<byte[]> units, nint[] textures)
    {
        var calls = new List<double>();
        var allocated = 0L;

        for (var tick = 0; tick < Units * 2; tick++)
        {
            if (tick % 2 == 0)
            {
                var unit = units[tick / 2];
                source.Received(unit, (uint)(tick / 2) * RtpPerUnit, AnnexBUnits.IsKeyFrame(unit));
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            shown.Shown(device, textures[tick % textures.Length], Width, Height);
            var spent = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            calls.Add(spent);

            // The real refresh's pace, so the encoder keeps up as it does in a stream.
            _clock.Advance(TimeSpan.FromSeconds(1.0 / ShownRate));
            Thread.Sleep(1000 / ShownRate);
        }

        _output.WriteLine($"render thread per shown picture: average {calls.Skip(1).Average():F3} ms, worst {calls.Skip(1).Max():F3} ms; managed allocation {allocated} bytes over {calls.Count} pictures");
        Assert.Equal(0, allocated);
    }

    private static void AssertSource(SourceOutcome outcome)
    {
        Assert.Null(outcome.Problem);
        Assert.NotNull(outcome.Facts);
        Assert.Equal((Units, Width, Height, "H.264"), (outcome.Facts.Frames, outcome.Facts.Width, outcome.Facts.Height, outcome.Facts.Codec));
        Assert.InRange(outcome.Facts.Duration.TotalSeconds, 0.98, 1.02);
        Assert.Equal((0L, 0L), (outcome.Dropped, outcome.Failed));
    }

    private static void AssertShown(ShownOutcome outcome, string path)
    {
        Assert.Null(outcome.Problem);
        Assert.NotNull(outcome.Facts);
        Assert.Equal(Units * 2, outcome.Written + outcome.Busy + outcome.Dropped);
        Assert.Equal(outcome.Written, outcome.Facts.Frames);
        Assert.InRange(outcome.Facts.Duration.TotalSeconds, 0.95, 1.02);
        Assert.Equal(TimeSpan.Zero, outcome.Offset);

        var lumas = DecodedLuma.AtCentre(path);

        if (outcome.Busy + outcome.Dropped == 0)
        {
            for (var frame = 0; frame < lumas.Count; frame++)
            {
                Assert.InRange(lumas[frame], Lumas[frame % Lumas.Length] - LumaTolerance, Lumas[frame % Lumas.Length] + LumaTolerance);
            }
        }
    }

    // Where the user has ffmpeg: both comparisons, read back at the pair's size and their lengths.
    private void Compare(string folder, SourceOutcome source, ShownOutcome shown)
    {
        var places = RecordingPlaces.Find();

        if (places.Ffmpeg is null)
        {
            _output.WriteLine($"comparison not checked: {FfmpegLocator.NotFound}");

            return;
        }

        var request = new CompareRequest(Path.Combine(folder, RecordingFolder.SourceFile), Path.Combine(folder, RecordingFolder.ShownFile), Path.Combine(folder, RecordingFolder.CompareFile), shown.Offset, Width, Height, ShownRate, "console 30 fps", "Relay LSFG x2", places.FontFile, HalfSpeed: false);
        var slow = request with { Output = Path.Combine(folder, RecordingFolder.HalfSpeedFile), HalfSpeed = true };
        var runner = new CompareRunner(places.Ffmpeg);
        var percents = new List<int>();
        var started = Stopwatch.GetTimestamp();

        Assert.Null(runner.Run(request, source.Facts!.Duration, percents.Add));
        Assert.Null(runner.Run(slow, source.Facts.Duration * 2, percents.Add));
        _output.WriteLine($"both comparisons in {Stopwatch.GetElapsedTime(started).TotalSeconds:F2} s; progress {string.Join(' ', percents)}");

        var compare = Mp4Reader.TryRead(request.Output, out var problem);
        var half = Mp4Reader.TryRead(slow.Output, out problem);
        Assert.True(compare is not null && half is not null, problem);
        _output.WriteLine($"compare: {compare}; half speed: {half}");

        Assert.Equal((Width * 2, Height), (compare.Width, compare.Height));
        Assert.InRange(compare.Duration.TotalSeconds, 0.9, 1.1);
        Assert.InRange(half.Duration.TotalSeconds, 1.8, 2.2);
    }

    private static nint FlatTexture(StrategyBasedComWrappers wrappers, nint device, byte luma)
    {
        const byte Neutral = 128;

        var bytes = new byte[Width * Height * 3 / 2];
        bytes.AsSpan(0, Width * Height).Fill(luma);
        bytes.AsSpan(Width * Height).Fill(Neutral);
        var description = new Texture2DDescription { Width = Width, Height = Height, MipLevels = 1, ArraySize = 1, Format = MediaFoundationValues.Nv12Format, SampleCount = 1, Usage = Direct3D11Values.DefaultUsage, BindFlags = Direct3D11Values.BindShaderResource };
        var api = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        nint texture;

        fixed (byte* memory = bytes)
        {
            var data = new SubresourceData { Memory = (nint)memory, RowPitch = Width, SlicePitch = (uint)bytes.Length };
            Assert.True(api.CreateTexture2D(in description, (nint)(&data), out texture) >= 0);
        }

        ComObjects.Free(api);

        return texture;
    }

    private static void Release(nint[] textures)
    {
        for (var index = 0; index < textures.Length; index++)
        {
            ComObjects.Release(ref textures[index]);
        }
    }
}
