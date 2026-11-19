// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Tests.Input;

public sealed class PadReportPolicyTests
{
    private static readonly XboxPadState Jump = new(XboxButtons.A, 0f, 0f, 0f, 0f, 0f, 0f);

    private static readonly XboxPadState Tilted = new(XboxButtons.None, 0.5f, 0f, 0f, 0f, 0f, 0f);

    [Fact]
    public void The_standard_keeps_alive_every_100_ms_with_one_axis_step_as_epsilon()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), PadReportSettings.Standard.KeepAlive);
        Assert.Equal(1f / 32767f, PadReportSettings.Standard.StickEpsilon);
        Assert.True(PadReportSettings.Standard.NeutralOnFocusLoss);
    }

    [Fact]
    public void The_first_pad_and_every_change_go_out_at_once()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard);

        var first = policy.ShouldSend(default, true, Ms(0), out _);
        var pressed = policy.ShouldSend(Jump, true, Ms(1), out var report);
        var released = policy.ShouldSend(default, true, Ms(2), out _);

        Assert.True(first);
        Assert.True(pressed);
        Assert.Equal(Jump, report);
        Assert.True(released);
    }

    [Fact]
    public void An_unchanged_pad_waits_for_the_keep_alive()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard);
        policy.ShouldSend(Tilted, true, Ms(0), out _);

        var early = policy.ShouldSend(Tilted, true, Ms(99), out _);
        var due = policy.ShouldSend(Tilted, true, Ms(100), out var report);
        var afterRepeat = policy.ShouldSend(Tilted, true, Ms(150), out _);

        Assert.False(early);
        Assert.True(due);
        Assert.Equal(Tilted, report);
        Assert.False(afterRepeat);
    }

    [Fact]
    public void The_keep_alive_is_configurable()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard with { KeepAlive = TimeSpan.FromMilliseconds(20) });
        policy.ShouldSend(Tilted, true, Ms(0), out _);

        Assert.False(policy.ShouldSend(Tilted, true, Ms(19), out _));
        Assert.True(policy.ShouldSend(Tilted, true, Ms(20), out _));
    }

    [Fact]
    public void A_move_below_the_epsilon_is_no_change_and_one_at_it_is()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard with { StickEpsilon = 0.01f });
        policy.ShouldSend(Tilted, true, Ms(0), out _);

        var small = policy.ShouldSend(Tilted with { LeftX = 0.505f }, true, Ms(1), out _);
        var drifted = policy.ShouldSend(Tilted with { LeftX = 0.511f }, true, Ms(2), out _);
        var trigger = policy.ShouldSend(Tilted with { LeftX = 0.511f, RightTrigger = 0.02f }, true, Ms(3), out _);

        Assert.False(small);
        Assert.True(drifted);
        Assert.True(trigger);
    }

    [Fact]
    public void One_axis_step_is_a_change_by_default()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard);
        policy.ShouldSend(Tilted, true, Ms(0), out _);

        var step = policy.ShouldSend(Tilted with { RightY = 1f / 32767f }, true, Ms(1), out _);
        var halfStep = policy.ShouldSend(Tilted with { RightY = 1.5f / 32767f }, true, Ms(2), out _);

        Assert.True(step);
        Assert.False(halfStep);
    }

    [Fact]
    public void Losing_focus_sends_the_pad_at_rest_once()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard);
        policy.ShouldSend(Jump, true, Ms(0), out _);

        var lost = policy.ShouldSend(Jump, false, Ms(1), out var report);
        var stillAway = policy.ShouldSend(Tilted, false, Ms(2), out _);
        var back = policy.ShouldSend(Tilted, true, Ms(3), out var resumed);

        Assert.True(lost);
        Assert.Equal(default, report);
        Assert.False(stillAway);
        Assert.True(back);
        Assert.Equal(Tilted, resumed);
    }

    [Fact]
    public void Without_the_rule_the_pad_plays_in_the_background()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard with { NeutralOnFocusLoss = false });
        policy.ShouldSend(Jump, true, Ms(0), out _);

        var away = policy.ShouldSend(Tilted, false, Ms(1), out var report);

        Assert.True(away);
        Assert.Equal(Tilted, report);
    }

    [Fact]
    public void A_reset_sends_the_next_pad_at_once()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard);
        policy.ShouldSend(Tilted, true, Ms(0), out _);

        policy.Reset();

        Assert.True(policy.ShouldSend(Tilted, true, Ms(1), out _));
    }

    [Fact]
    public void Deciding_allocates_nothing()
    {
        var policy = new PadReportPolicy(PadReportSettings.Standard);
        var now = TimeSpan.Zero;
        var step = TimeSpan.FromMilliseconds(4);

        var report = AllocationMeter.Measure(() =>
        {
            now += step;
            policy.ShouldSend(Tilted, true, now, out _);
        });

        Assert.Equal(0, report.BytesPerRun);
    }

    private static TimeSpan Ms(int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
