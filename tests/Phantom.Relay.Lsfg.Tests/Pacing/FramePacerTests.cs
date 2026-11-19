// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Lsfg.Pacing;

namespace Phantom.Relay.Lsfg.Tests.Pacing;

/// <summary>The pacer on a manual clock: 30 fps real frames on a 60 Hz or 120 Hz display.</summary>
public sealed class FramePacerTests
{
    private static readonly TimeSpan Source = TimeSpan.FromSeconds(1.0 / 30);
    private static readonly TimeSpan Refresh60 = TimeSpan.FromSeconds(1.0 / 60);
    private static readonly TimeSpan Refresh120 = TimeSpan.FromSeconds(1.0 / 120);

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Nothing_is_shown_before_the_first_frame()
    {
        var pacer = new FramePacer(_clock, 2, Source, Refresh60);

        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
    }

    [Fact]
    public void The_first_frame_is_shown_at_once_and_then_held()
    {
        var pacer = new FramePacer(_clock, 2, Source, Refresh60);

        pacer.OnRealFrame(0);

        Assert.Equal(new PacedFrame(PacedFrameKind.Real, 0, 0, 2), pacer.OnRefresh());
        _clock.Advance(Refresh60);
        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
    }

    [Fact]
    public void At_x2_on_60_hz_the_generated_frame_comes_first_then_the_real_one_a_refresh_later()
    {
        var pacer = Started(2, Refresh60);

        pacer.OnRealFrame(1);
        _clock.Advance(TimeSpan.FromMilliseconds(3));
        pacer.OnGeneratedReady(1, 1);

        Assert.Equal(new PacedFrame(PacedFrameKind.Generated, 1, 1, 2), pacer.OnRefresh());
        _clock.Advance(Refresh60);
        Assert.Equal(new PacedFrame(PacedFrameKind.Real, 1, 0, 2), pacer.OnRefresh());
        _clock.Advance(Refresh60);
        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
    }

    [Fact]
    public void A_generated_frame_is_shown_as_soon_as_it_is_ready()
    {
        var pacer = Started(2, Refresh60);

        pacer.OnRealFrame(1);

        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
        _clock.Advance(TimeSpan.FromMilliseconds(4));
        pacer.OnGeneratedReady(1, 1);
        Assert.Equal(PacedFrameKind.Generated, pacer.OnRefresh().Kind);
    }

    [Fact]
    public void A_real_frame_is_never_held_back_a_whole_source_frame_when_generation_is_missing()
    {
        var pacer = Started(2, Refresh60);

        pacer.OnRealFrame(1);
        var shownAt = TimeSpan.Zero;

        while (pacer.OnRefresh().Kind != PacedFrameKind.Real)
        {
            _clock.Advance(Refresh60 / 4);
            shownAt += Refresh60 / 4;
        }

        Assert.True(shownAt < Source, $"real frame shown {shownAt.TotalMilliseconds} ms after it arrived");
    }

    [Fact]
    public void A_generated_frame_that_is_late_when_the_real_one_is_due_is_skipped()
    {
        var pacer = Started(2, Refresh60);

        pacer.OnRealFrame(1);
        _clock.Advance(Refresh60);
        pacer.OnGeneratedReady(1, 1);

        Assert.Equal(new PacedFrame(PacedFrameKind.Real, 1, 0, 2), pacer.OnRefresh());
        _clock.Advance(Refresh60);
        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
    }

    [Fact]
    public void At_x4_on_120_hz_the_three_generated_frames_come_in_order_then_the_real_one()
    {
        var pacer = Started(4, Refresh120);

        pacer.OnRealFrame(1);
        pacer.OnGeneratedReady(1, 1);
        pacer.OnGeneratedReady(1, 2);
        pacer.OnGeneratedReady(1, 3);
        var shown = new List<PacedFrame>();

        for (var refresh = 0; refresh < 4; refresh++)
        {
            shown.Add(pacer.OnRefresh());
            _clock.Advance(Refresh120);
        }

        Assert.Equal(
            [
                new PacedFrame(PacedFrameKind.Generated, 1, 1, 4),
                new PacedFrame(PacedFrameKind.Generated, 1, 2, 4),
                new PacedFrame(PacedFrameKind.Generated, 1, 3, 4),
                new PacedFrame(PacedFrameKind.Real, 1, 0, 4),
            ],
            shown);
    }

    [Fact]
    public void A_new_real_frame_drops_what_was_still_pending_of_the_pair_before()
    {
        var pacer = Started(4, Refresh120);

        pacer.OnRealFrame(1);
        pacer.OnGeneratedReady(1, 1);
        Assert.Equal(PacedFrameKind.Generated, pacer.OnRefresh().Kind);
        _clock.Advance(Refresh120);
        pacer.OnRealFrame(2);
        pacer.OnGeneratedReady(1, 2);

        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
        pacer.OnGeneratedReady(2, 1);
        Assert.Equal(new PacedFrame(PacedFrameKind.Generated, 2, 1, 4), pacer.OnRefresh());
    }

    [Fact]
    public void The_interval_follows_the_arrivals()
    {
        var pacer = new FramePacer(_clock, 2, Source, Refresh60);
        var actual = TimeSpan.FromMilliseconds(40);

        for (var frame = 0; frame < 60; frame++)
        {
            pacer.OnRealFrame(frame);
            _clock.Advance(actual);
        }

        Assert.InRange(pacer.SourceInterval.TotalMilliseconds, 39, 41);
    }

    [Fact]
    public void A_pair_25_fps_apart_at_the_source_shows_its_real_frame_half_its_own_interval_later()
    {
        var pacer = Started(2, Refresh120);
        var pair = TimeSpan.FromMilliseconds(50);

        pacer.OnRealFrame(1, pair);
        _clock.Advance(TimeSpan.FromMilliseconds(18));

        // At 33 ms nominal the real frame would be due by now (16.7 ms); this pair's is due at 25 ms.
        Assert.Equal(PacedFrame.Hold, pacer.OnRefresh());
        _clock.Advance(TimeSpan.FromMilliseconds(4));
        Assert.Equal(PacedFrameKind.Real, pacer.OnRefresh().Kind);
        Assert.Equal(50, pacer.SourceInterval.TotalMilliseconds, 1);
    }

    [Fact]
    public void A_pair_after_a_long_hold_is_spread_over_two_nominal_intervals_at_most()
    {
        var pacer = Started(2, Refresh60);

        pacer.OnRealFrame(1, TimeSpan.FromMilliseconds(100));

        Assert.Equal(Source.TotalMilliseconds * 2, pacer.SourceInterval.TotalMilliseconds, 1);
    }

    [Fact]
    public void Without_a_source_interval_the_pair_follows_the_arrivals()
    {
        var pacer = Started(2, Refresh60);

        pacer.OnRealFrame(1, TimeSpan.Zero);

        Assert.Equal(Source.TotalMilliseconds, pacer.SourceInterval.TotalMilliseconds, 1);
    }

    [Fact]
    public void The_refresh_is_measured_from_the_calls_and_a_stall_does_not_count()
    {
        var pacer = new FramePacer(_clock, 2, Source, Refresh60);
        var refresh240 = TimeSpan.FromSeconds(1.0 / 240);

        for (var refresh = 0; refresh < 120; refresh++)
        {
            _ = pacer.OnRefresh();
            _clock.Advance(refresh240);
        }

        _clock.Advance(TimeSpan.FromMilliseconds(200));
        _ = pacer.OnRefresh();

        Assert.Equal(refresh240.TotalMilliseconds, pacer.RefreshInterval.TotalMilliseconds, 1);
    }

    // A pacer that has shown frame 0 and is a source interval later, so frame 1 starts the first pair.
    private FramePacer Started(int multiplier, TimeSpan refresh)
    {
        var pacer = new FramePacer(_clock, multiplier, Source, refresh);
        pacer.OnRealFrame(0);
        _ = pacer.OnRefresh();
        _clock.Advance(Source);

        return pacer;
    }
}
