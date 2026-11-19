// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows.Interop;
using Xunit.Abstractions;

namespace Phantom.Relay.Media.Windows.Tests;

/// <summary>
/// The decoder's GPU path on this machine: each picture of a 1440p stream copied on the GPU into a ring of shared NV12
/// textures and its fence signalled, against the read-back path's time on the CPU; the last slot read back and checked
/// against the gradient. The renderer's import of the handles is Phantom.Engine's, checked by its own live test.
/// </summary>
[Trait("Category", "Live")]
public sealed unsafe class SharedSurfaceRingLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;
    private const int Frames = 30;
    private const int Slots = 3;
    private const long FrameNanoseconds = SyntheticH264Encoding.FrameUnits * 100;

    private readonly ITestOutputHelper _output;

    public SharedSurfaceRingLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Each_picture_is_copied_into_a_shared_slot_on_the_gpu_and_the_fence_marks_it()
    {
        var units = SyntheticH264Encoding.Encode(Width, Height, Frames);
        using var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, out var problem);
        Assert.True(decoder is not null, problem);
        Assert.True(decoder.IsHardwareAccelerated, decoder.FallbackReason);

        SharedSurfaceRing? ring = null;
        var handOff = new List<double>();
        var allocated = 0L;

        try
        {
            for (var index = 0; index < units.Count; index++)
            {
                Assert.Equal(VideoDecodeResult.Picture, decoder.Decode(units[index], index * FrameNanoseconds));
                Assert.True(decoder.TryTakeSurface(out var texture, out var slice));
                ring ??= Ring(decoder, texture);

                var before = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                Assert.True(ring.WaitForReader(ring.FenceHandle, ring.Written));
                ring.Copy(index % Slots, texture, slice);
                ring.Signal();

                handOff.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }

            AssertSlot(decoder, ring!, (Frames - 1) % Slots, Frames - 1);
            _output.WriteLine($"GPU hand-off (wait, copy, signal) on the CPU: average {handOff.Skip(1).Average():F3} ms, worst {handOff.Skip(1).Max():F3} ms; managed allocation {allocated} bytes over {Frames} pictures");
            _output.WriteLine($"ring: {ring!.Count} slots of {ring.Width}x{ring.Height}, render targets: {ring.IsWritableByVideoProcessor}");
        }
        finally
        {
            ring?.Dispose();
        }
    }

    [Fact]
    public void An_adapter_luid_no_gpu_has_falls_back_to_software_and_says_why()
    {
        const long NoSuchAdapter = 0x7FFF_0000_7FFF;

        using var decoder = MediaFoundationH264Decoder.TryOpen(TimeProvider.System, preferHardware: true, NoSuchAdapter, out var problem);

        Assert.True(decoder is not null, problem);
        Assert.False(decoder.IsHardwareAccelerated);
        Assert.Contains("LUID", decoder.FallbackReason, StringComparison.Ordinal);
    }

    private static SharedSurfaceRing Ring(MediaFoundationH264Decoder decoder, nint texture)
    {
        var wrappers = new StrategyBasedComWrappers();
        var api = ComObjects.Wrap<ID3D11Texture2D>(wrappers, texture);
        api.GetDesc(out var description);
        ComObjects.Free(api);

        var ring = SharedSurfaceRing.TryCreate(decoder.Device, Slots, (int)description.Width, (int)description.Height, out var problem);
        Assert.True(ring is not null, problem);

        return ring;
    }

    // The slot read back through a staging texture on the decoder's device: its luma is the gradient's.
    private static void AssertSlot(MediaFoundationH264Decoder decoder, SharedSurfaceRing ring, int slot, int frame)
    {
        var wrappers = new StrategyBasedComWrappers();
        var device = ComObjects.Wrap<ID3D11Device>(wrappers, decoder.Device);
        device.GetImmediateContext(out var contextPointer);
        var context = ComObjects.Wrap<ID3D11DeviceContext>(wrappers, contextPointer);
        var description = new Texture2DDescription { Width = (uint)ring.Width, Height = (uint)ring.Height, MipLevels = 1, ArraySize = 1, Format = MediaFoundationValues.Nv12Format, SampleCount = 1, Usage = MediaFoundationValues.StagingUsage, CpuAccessFlags = MediaFoundationValues.CpuRead };
        Assert.True(device.CreateTexture2D(in description, 0, out var staging) >= 0);

        context.CopyResource(staging, ring.Texture(slot));
        Assert.True(context.Map(staging, 0, MediaFoundationValues.MapRead, 0, out var mapped) >= 0);

        foreach (var (x, y) in new[] { (100, 60), (1280, 720), (2400, 1300) })
        {
            var actual = ((byte*)mapped.Data)[(y * mapped.RowPitch) + x];
            var expected = SyntheticH264Encoding.LumaAt(x, y, frame);
            Assert.True(Math.Abs(expected - actual) <= 16, $"luma at {x},{y}: {actual}, the gradient has {expected}");
        }

        context.Unmap(staging, 0);
        Assert.True(SharingCalls.CompletedValue(OpenOwnFence(decoder, ring)) >= ring.Written);
        ComObjects.Release(ref staging);
        ComObjects.Free(context);
        ComObjects.Free(device);
        ComObjects.Release(ref contextPointer);
    }

    private static nint OpenOwnFence(MediaFoundationH264Decoder decoder, SharedSurfaceRing ring)
    {
        var device5 = SharingCalls.Query(decoder.Device, in SharingCalls.Device5);
        Assert.True(SharingCalls.OpenSharedFence(device5, ring.FenceHandle, out var fence) >= 0);
        _ = Marshal.Release(device5);

        return fence;
    }
}
