// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Stream.Tests.Media.Gpu;

public sealed class GpuPictureSlotsTests
{
    private const nint RendererFence = 77;

    [Fact]
    public void A_picture_published_is_taken_once_and_its_slot_is_on_screen()
    {
        var queue = new GpuPictureSlots(3, 1, new NewestPicturePacer());

        var slot = Write(queue, ready: 1);

        Assert.True(queue.TryTake(out var picture));
        Assert.Equal((slot, 1ul), (picture.Slot, picture.ReadyValue));
        Assert.Equal(GpuSlotState.Shown, queue.StateOf(slot));
        Assert.False(queue.TryTake(out _));
    }

    [Fact]
    public void The_producer_waits_for_the_renderers_last_read_of_a_slot_it_writes_again()
    {
        var queue = new GpuPictureSlots(3, 1, new NewestPicturePacer());
        var first = Write(queue, ready: 1);
        Assert.True(queue.TryTake(out var shown));
        queue.Release(shown, new GpuSlotRelease(RendererFence, 40));
        queue.Release(shown, new GpuSlotRelease(RendererFence, 41));

        Write(queue, ready: 2);
        Assert.True(queue.TryTake(out _));

        // The two other slots first (freed longest ago), then the first, with the renderer's last read of it.
        Assert.True(queue.TryClaim(out var next, out var release));
        Assert.NotEqual(first, next);
        queue.Abandon(next);
        Assert.True(queue.TryClaim(out next, out release));
        Assert.Equal(first, next);
        Assert.Equal(new GpuSlotRelease(RendererFence, 41), release);
    }

    [Fact]
    public void A_new_group_frees_the_pending_one_the_renderer_had_no_refresh_for()
    {
        var queue = new GpuPictureSlots(3, 1, new NewestPicturePacer());
        var skipped = Write(queue, ready: 1);

        var newest = Write(queue, ready: 2);

        Assert.Equal(GpuSlotState.Free, queue.StateOf(skipped));
        Assert.True(queue.TryTake(out var picture));
        Assert.Equal(newest, picture.Slot);
    }

    [Fact]
    public void Slots_on_screen_or_pending_are_never_claimed_however_fast_the_producer_runs()
    {
        var queue = new GpuPictureSlots(3, 1, new NewestPicturePacer());
        Write(queue, ready: 1);
        Assert.True(queue.TryTake(out var shown));

        for (var picture = 2; picture < 100; picture++)
        {
            var slot = Write(queue, ready: (ulong)picture);

            Assert.NotEqual(shown.Slot, slot);
        }
    }

    [Fact]
    public void Groups_of_frame_generation_fit_in_twice_the_group_and_one()
    {
        var pacer = new ScriptedPacer();
        var queue = new GpuPictureSlots(5, 2, pacer);
        WriteGroup(queue, 2);
        pacer.Next = 0;
        Assert.True(queue.TryTake(out var generated));

        WriteGroup(queue, 2);
        WriteGroup(queue, 2);

        Assert.Equal(3, queue.Groups);
        Assert.Equal(GpuSlotState.Shown, queue.StateOf(generated.Slot));
        Assert.Equal(2, pacer.LastCount);
    }

    [Fact]
    public void A_pacer_that_holds_keeps_the_picture_on_screen()
    {
        var pacer = new ScriptedPacer();
        var queue = new GpuPictureSlots(3, 1, pacer);
        Write(queue, ready: 1);

        pacer.Next = GpuPictureSlots.Hold;

        Assert.False(queue.TryTake(out _));
    }

    [Fact]
    public void After_a_new_ring_the_old_rings_release_is_ignored_and_every_slot_is_free()
    {
        var queue = new GpuPictureSlots(3, 1, new NewestPicturePacer());
        Write(queue, ready: 1);
        Assert.True(queue.TryTake(out var old));

        queue.Reset(1);
        queue.Release(old, new GpuSlotRelease(RendererFence, 9));

        Assert.Equal(GpuSlotState.Free, queue.StateOf(old.Slot));
        Assert.True(queue.TryClaim(out _, out var release));
        Assert.Equal(default, release);
    }

    [Fact]
    public void Fewer_slots_than_two_groups_and_one_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GpuPictureSlots(4, 2, new NewestPicturePacer()));
    }

    private static int Write(GpuPictureSlots queue, ulong ready)
    {
        Assert.True(queue.TryClaim(out var slot, out _));
        queue.Add(Picture(slot, ready));
        queue.Publish();

        return slot;
    }

    private static void WriteGroup(GpuPictureSlots queue, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Assert.True(queue.TryClaim(out var slot, out _));
            queue.Add(Picture(slot, (ulong)index));
        }

        queue.Publish();
    }

    private static GpuPicture Picture(int slot, ulong ready)
    {
        return new GpuPicture(0, slot, ready, 2560, 1440, VideoColour.Default, 0, false);
    }
}
