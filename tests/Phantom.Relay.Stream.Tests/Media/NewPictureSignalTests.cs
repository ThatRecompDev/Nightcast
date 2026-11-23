// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

public sealed class NewPictureSignalTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly NewPictureSignal _signal = new();

    public void Dispose()
    {
        _signal.Dispose();
    }

    [Fact]
    public void Without_a_new_picture_the_wait_runs_out()
    {
        Assert.False(_signal.Wait(TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void A_repeat_is_no_new_picture()
    {
        _signal.Arrived(PictureKind.Repeat);

        Assert.False(_signal.Wait(TimeSpan.Zero));
    }

    [Fact]
    public void New_pictures_that_arrived_before_the_wait_count_once()
    {
        _signal.Arrived(PictureKind.New);
        _signal.Arrived(PictureKind.New);

        Assert.True(_signal.Wait(TimeSpan.Zero));
        Assert.False(_signal.Wait(TimeSpan.Zero));
    }

    [Fact]
    public void A_picture_arriving_during_the_wait_ends_it()
    {
        var decoder = new Thread(() =>
        {
            Thread.Sleep(20);
            _signal.Arrived(PictureKind.New);
        });

        decoder.Start();
        var arrived = _signal.Wait(Patience);
        decoder.Join();

        Assert.True(arrived);
    }

    [Fact]
    public void Waiting_and_signalling_allocate_nothing()
    {
        var signal = _signal;

        var report = AllocationMeter.Measure(() =>
        {
            signal.Arrived(PictureKind.New);
            signal.Wait(TimeSpan.Zero);
        });

        Assert.Equal(0, report.BytesPerRun);
    }
}
