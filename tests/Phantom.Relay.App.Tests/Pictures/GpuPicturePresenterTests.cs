// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Media.Video;
using Phantom.Engine.Testing.Graphics;
using Phantom.Relay.App.Pictures;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Tests.Pictures;

public sealed class GpuPicturePresenterTests
{
    private const long Luid = 0x0001_0000_D00D;
    private const nint FenceHandle = 0x50;

    private static readonly GpuCapabilities Sharing = RecordingGraphicsDevice.WebGpu with { TextureFeatures = GpuTextureFeatures.SharedTextures, AdapterLuid = Luid };
    private static readonly Vector4 Whole = new(0f, 0f, 1f, 1f);

    private readonly FakeGpuPictures _source = new();

    [Fact]
    public void A_device_that_shares_offers_its_adapter_and_one_that_does_not_says_why()
    {
        var sharing = new GpuPicturePresenter(new RecordingGraphicsDevice(Sharing), _source);
        var plain = new GpuPicturePresenter(new RecordingGraphicsDevice(RecordingGraphicsDevice.WebGpu), new FakeGpuPictures());

        sharing.Initialize();
        plain.Initialize();

        Assert.Equal(Luid, _source.OfferedLuid);
        Assert.Null(sharing.Refusal);
        Assert.Contains("no shared textures", plain.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_frame_draws_the_picture_inside_its_access_and_hands_the_reads_end_back()
    {
        var (device, presenter) = Ready(generation: 1);
        _source.Add(Picture(generation: 1, slot: 2, ready: 7));

        Assert.True(presenter.TryTake());
        Frame(device, presenter);
        Frame(device, presenter);

        Assert.Equal(2, _source.Releases.Count);
        Assert.All(_source.Releases, release => Assert.Equal((2, RecordingGraphicsDevice.SharedFenceHandle), (release.Picture.Slot, release.Fence)));
        Assert.Equal(3, device.Calls.Count(call => call.Kind == GpuCallKind.ImportSharedTexture));
    }

    [Fact]
    public void A_new_ring_is_opened_and_the_old_one_let_go()
    {
        var (device, presenter) = Ready(generation: 1);
        _source.Add(Picture(generation: 1, slot: 0, ready: 1));
        presenter.TryTake();
        Frame(device, presenter);

        _source.Ring = Ring(generation: 2, firstHandle: 0x200);
        _source.Add(Picture(generation: 2, slot: 0, ready: 1));
        presenter.TryTake();
        Frame(device, presenter);

        Assert.Equal(6, device.Calls.Count(call => call.Kind == GpuCallKind.ImportSharedTexture));
        Assert.Equal(2, device.Calls.Count(call => call.Kind == GpuCallKind.ImportSharedFence));
    }

    [Fact]
    public void A_ring_the_device_refuses_sends_the_pictures_back_to_the_cpu()
    {
        var device = new RecordingGraphicsDevice(Sharing);
        var presenter = new GpuPicturePresenter(device, _source);
        presenter.Initialize();
        _source.Ring = Ring(generation: 1, firstHandle: 0x100);
        _source.Add(Picture(generation: 1, slot: 0, ready: 1));
        presenter.TryTake();

        device.BeginFramePass(Vector4.Zero);

        Assert.False(presenter.Draw(Whole));
        Assert.NotNull(_source.Withdrawn);
        Assert.False(presenter.HasPicture);
    }

    private static GpuPicture Picture(int generation, int slot, ulong ready)
    {
        return new GpuPicture(generation, slot, ready, 1920, 1080, VideoColour.Default, 0, false);
    }

    private static GpuPictureRing Ring(int generation, nint firstHandle)
    {
        return new GpuPictureRing(generation, [firstHandle, firstHandle + 1, firstHandle + 2], FenceHandle, 1920, 1088);
    }

    private static void Frame(RecordingGraphicsDevice device, GpuPicturePresenter presenter)
    {
        device.BeginFramePass(Vector4.Zero);
        Assert.True(presenter.Draw(Whole));
        device.EndPass();
        device.Commit();
        presenter.EndFrame();
    }

    private (RecordingGraphicsDevice Device, GpuPicturePresenter Presenter) Ready(int generation)
    {
        var device = new RecordingGraphicsDevice(Sharing);
        _source.Ring = Ring(generation, 0x100);
        Share(device, _source.Ring);
        Share(device, Ring(generation + 1, 0x200));

        var presenter = new GpuPicturePresenter(device, _source);
        presenter.Initialize();

        return (device, presenter);
    }

    private static void Share(RecordingGraphicsDevice device, GpuPictureRing ring)
    {
        foreach (var handle in ring.Slots)
        {
            device.Share(handle, ring.TextureWidth, ring.TextureHeight, SharedTextureFormat.Nv12);
        }

        device.Share(ring.Fence, 1, 1, SharedTextureFormat.Rgba8);
    }
}
