// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Media.Windows.Interop;
using Phantom.Relay.Media.Windows.Tests;
using Xunit.Abstractions;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// Where a new picture's frame-generation time goes on the decoder's own device, as the stream runs it: a 1440p H.264
/// stream decoded by the hardware decoder, every other unit a new picture through the stage's steps (NV12 to RGBA, the
/// real frame's passes, the generated frame's, RGBA to NV12 into a shared slot, then the decoded picture's copy), each
/// step's end marked with a GPU timestamp. Run back to back and paced at the console's 60 units a second, with the
/// bracket opened right after the decode (as the stream does) or only once the decode is done on the GPU.
/// </summary>
[Trait("Category", "Live")]
public sealed unsafe class LsfgCostLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int Units = 120;
    private const int WarmUp = 10;
    private const int Slots = 5;
    private const long FrameNanoseconds = SyntheticH264Encoding.FrameUnits * 100;
    private const int Marks = 6;

    private static readonly string[] Steps = ["NV12 to RGBA", "real frame's passes", "generated frame's passes", "RGBA to NV12", "decoded copy"];

    private static List<byte[]>? _units;

    private readonly ITestOutputHelper _output;
    private readonly TestOutputLogger _log;

    public LsfgCostLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _log = new TestOutputLogger(output);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 16)]
    [InlineData(true, 16)]
    public void A_new_pictures_gpu_time_split_by_step(bool drained, int unitMilliseconds)
    {
        var units = _units ??= SyntheticH264Encoding.Encode(Width, Height, Units);
        using var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, out var problem);
        Assert.True(decoder is not null, problem);
        Assert.True(decoder.IsHardwareAccelerated, decoder.FallbackReason);

        using var parts = Parts.Create(decoder.Device, _log);
        var totals = new double[Marks];
        var counted = 0;
        var waits = new List<double>();
        var views = new HashSet<(nint, uint)>();

        for (var index = 0; index < units.Count; index++)
        {
            var started = Stopwatch.GetTimestamp();
            Assert.Equal(Phantom.Engine.Media.Video.VideoDecodeResult.Picture, decoder.Decode(units[index], index * FrameNanoseconds));
            Assert.True(decoder.TryTakeSurface(out var surface, out var slice));
            views.Add((surface, slice));
            parts.EnsureRing(surface);

            if (drained)
            {
                waits.Add(parts.WaitForDecode(surface, slice));
            }

            var times = index % 2 == 0 ? parts.Picture(surface, slice) : parts.Repeat(surface, slice);

            if (times is not null && index >= WarmUp)
            {
                Add(totals, times);
                counted++;
            }

            Pace(started, unitMilliseconds);
        }

        Report(drained, unitMilliseconds, totals, counted, waits, views.Count);
    }

    [Fact]
    public void Paced_like_the_stream_for_ten_seconds_the_gpu_time_per_second()
    {
        var units = _units ??= SyntheticH264Encoding.Encode(Width, Height, Units);
        using var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, out var problem);
        Assert.True(decoder is not null, problem);

        using var parts = Parts.Create(decoder.Device, _log);
        var second = new List<double>();
        var clock = Stopwatch.StartNew();

        for (var index = 0; clock.Elapsed < TimeSpan.FromSeconds(10); index++)
        {
            var started = Stopwatch.GetTimestamp();
            var unit = index % units.Count;
            _ = decoder.Decode(units[unit], index * FrameNanoseconds);
            Assert.True(decoder.TryTakeSurface(out var surface, out var slice));
            parts.EnsureRing(surface);
            var times = index % 2 == 0 ? parts.Picture(surface, slice) : parts.Repeat(surface, slice);

            if (times is not null)
            {
                second.Add(times[Marks - 2]);
            }

            if (index % 60 == 59)
            {
                _output.WriteLine($"second {(index + 1) / 60}: the stage's bracket averages {second.Average():F3} ms, worst {second.Max():F3} ms");
                second.Clear();
            }

            Pace(started, 16);
        }
    }

    private static void Pace(long started, int unitMilliseconds)
    {
        var left = unitMilliseconds - Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (left > 0)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(left));
        }
    }

    private static void Add(double[] totals, double[] times)
    {
        for (var index = 1; index < Marks; index++)
        {
            totals[index] += times[index];
        }
    }

    private void Report(bool drained, int unitMilliseconds, double[] totals, int counted, List<double> waits, int surfaces)
    {
        var how = drained ? "after the decode is done" : "right after the decode (as the stream)";
        var pace = unitMilliseconds == 0 ? "back to back" : $"one unit every {unitMilliseconds} ms";
        _output.WriteLine($"x2 quality {Width}x{Height}, {pace}, bracket opened {how}; {counted} new pictures; {surfaces} decoder surfaces seen");

        for (var index = 1; index < Marks; index++)
        {
            var step = (totals[index] - totals[index - 1]) / counted;
            _output.WriteLine($"  {Steps[index - 1]}: {step:F3} ms (ends at {totals[index] / counted:F3} ms)");
        }

        _output.WriteLine($"  the stage's own bracket (to RGBA to NV12): {totals[Marks - 2] / counted:F3} ms");

        if (waits.Count > 0)
        {
            _output.WriteLine($"  CPU wait for each decode to finish on the GPU: average {waits.Average():F3} ms");
        }
    }

    /// <summary>The stage's steps, each its own object so a mark can go between them, on the decoder's device.</summary>
    private sealed class Parts : IDisposable
    {
        private readonly ComputeDevice _device;
        private readonly LsfgGenerator _generator;
        private readonly Nv12ToRgbaConverter _toRgba;
        private readonly RgbaToNv12Converter _toNv12;
        private readonly GpuMarks _marks;
        private readonly nint[] _outputs = new nint[1];
        private readonly double[] _times = new double[Marks];
        private SharedSurfaceRing? _ring;
        private nint _fence;
        private int _slot;

        private Parts(ComputeDevice device, LsfgGenerator generator, Nv12ToRgbaConverter toRgba, RgbaToNv12Converter toNv12)
        {
            _device = device;
            _generator = generator;
            _toRgba = toRgba;
            _toNv12 = toNv12;
            _marks = new GpuMarks(device, Marks);
        }

        public static Parts Create(nint device, TestOutputLogger log)
        {
            var path = LosslessLocator.Find(Environment.GetEnvironmentVariable("LSFG_LOSSLESS_DLL"), LosslessLocator.SteamLibraries());
            Assert.True(path is not null, "Lossless.dll was not found");
            using var catalog = LsfgShaderCatalog.TryLoad(path, log, out var problem);
            Assert.True(catalog is not null, problem);

            var settings = LsfgSettings.Default with { Enabled = true };
            var generator = LsfgGenerator.TryCreate(device, catalog, settings, Width, Height, LsfgFrameFormat.Rgba8, log, out problem);
            var toRgba = Nv12ToRgbaConverter.TryCreate(device, Width, Height, out problem);
            var toNv12 = RgbaToNv12Converter.TryCreate(device, Width, Height, out problem);
            Assert.True(generator is not null && toRgba is not null && toNv12 is not null, problem);

            return new Parts(ComputeDevice.Open(new StrategyBasedComWrappers(), device), generator, toRgba, toNv12);
        }

        public void EnsureRing(nint surface)
        {
            if (_ring is not null)
            {
                return;
            }

            _ring = SharedSurfaceRing.TryCreateFor(_device.Device, Slots, surface, out var problem);
            Assert.True(_ring is not null, problem);
            var device5 = SharingCalls.Query(_device.Device, in SharingCalls.Device5);
            Assert.True(SharingCalls.OpenSharedFence(device5, _ring.FenceHandle, out _fence) >= 0);
            _ = Marshal.Release(device5);
        }

        // The decoded picture copied and the fence set behind it, then waited for on the CPU: the decode is done.
        public double WaitForDecode(nint surface, uint slice)
        {
            var started = Stopwatch.GetTimestamp();
            _ring!.Copy(NextSlot(), surface, slice);
            var value = _ring.Signal();
            SharingCalls.Flush(_device.Context);

            while (SharingCalls.CompletedValue(_fence) < value)
            {
                Thread.Yield();
            }

            return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        // A new picture through every step, a mark after each; their times once the GPU is past them (null when disjoint).
        public double[]? Picture(nint surface, uint slice)
        {
            _marks.Begin();
            Assert.True(_toRgba.Convert(surface, slice));
            _marks.Mark(1);
            _generator.Submit(_toRgba.Output);
            _marks.Mark(2);

            var made = _generator.Generate(2, _outputs);
            _marks.Mark(3);

            if (made == 1)
            {
                Assert.True(_toNv12.Convert(_outputs[0], _ring!.Texture(NextSlot())));
            }

            _marks.Mark(4);
            Repeat(surface, slice);
            _marks.Mark(5);
            _marks.End();

            if (!_marks.Read(_times) || made == 0)
            {
                return null;
            }

            return _times;
        }

        // A repeat: decoded, copied, nothing generated.
        public double[]? Repeat(nint surface, uint slice)
        {
            _ring!.Copy(NextSlot(), surface, slice);
            _ = _ring.Signal();

            return null;
        }

        public void Dispose()
        {
            _marks.Dispose();
            ComObjects.Release(ref _fence);
            _ring?.Dispose();

            _toNv12.Dispose();
            _toRgba.Dispose();
            _generator.Dispose();
            _device.Dispose();
        }

        private int NextSlot()
        {
            _slot = (_slot + 1) % Slots;

            return _slot;
        }
    }
}
