// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class RecordingQueueTests
{
    [Fact]
    public void Items_come_out_in_order()
    {
        var queue = new RecordingQueue<int>(4);
        queue.TryAdd(1);
        queue.TryAdd(2);
        queue.TryAdd(3);

        var taken = Drain(queue);

        Assert.Equal([1, 2, 3], taken);
    }

    [Fact]
    public void A_full_queue_drops_and_counts_and_keeps_what_it_had()
    {
        var queue = new RecordingQueue<int>(2);
        var first = queue.TryAdd(1);
        var second = queue.TryAdd(2);

        var third = queue.TryAdd(3);
        var fourth = queue.TryAdd(4);

        Assert.True(first && second);
        Assert.False(third || fourth);
        Assert.Equal(2, queue.Dropped);
        Assert.Equal([1, 2], Drain(queue));
    }

    [Fact]
    public void Room_taken_out_is_room_again_around_the_ring()
    {
        var queue = new RecordingQueue<int>(2);

        for (var item = 0; item < 10; item++)
        {
            Assert.True(queue.TryAdd(item));
            Assert.True(queue.TryTake(out var taken, TimeSpan.Zero));
            Assert.Equal(item, taken);
        }

        Assert.Equal(0, queue.Dropped);
    }

    [Fact]
    public void A_completed_queue_takes_nothing_more_in_and_drains_what_is_left()
    {
        var queue = new RecordingQueue<int>(4);
        queue.TryAdd(7);

        queue.Complete();
        var refused = queue.TryAdd(8);

        Assert.False(refused);
        Assert.Equal(0, queue.Dropped);
        Assert.True(queue.IsCompleted);
        Assert.Equal([7], Drain(queue));
    }

    [Fact]
    public void An_empty_queue_answers_at_once_when_asked_not_to_wait()
    {
        var queue = new RecordingQueue<int>(1);

        Assert.False(queue.TryTake(out _, TimeSpan.Zero));
    }

    [Fact]
    public void Adding_allocates_nothing()
    {
        var queue = new RecordingQueue<int>(64);

        var report = AllocationMeter.Measure(() =>
        {
            queue.TryAdd(1);
            _ = queue.TryTake(out _, TimeSpan.Zero);
        });

        Assert.Equal(0, report.BytesPerRun);
    }

    private static List<int> Drain(RecordingQueue<int> queue)
    {
        var items = new List<int>();

        while (queue.TryTake(out var item, TimeSpan.Zero))
        {
            items.Add(item);
        }

        return items;
    }
}
