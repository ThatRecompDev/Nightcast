// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.App.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class RumblePlayerTests
{
    private const int DualSenseSlot = 2;

    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);

    private readonly Motors _motors = new();

    [Fact]
    public void A_report_runs_the_played_pads_motors_until_its_duration_ends()
    {
        var player = new RumblePlayer(_motors);

        player.Play(Report(0, 0.5f, 0.25f, 200), DualSenseSlot, Start);
        player.Tick(DualSenseSlot, Start + TimeSpan.FromMilliseconds(199));

        // At the starting gain of 200 %.
        Assert.Equal((DualSenseSlot, 1f, 0.5f), _motors.Last);
        Assert.True(player.Running);

        player.Tick(DualSenseSlot, Start + TimeSpan.FromMilliseconds(200));

        Assert.Equal((DualSenseSlot, 0f, 0f), _motors.Last);
        Assert.False(player.Running);
    }

    [Fact]
    public void A_report_of_zero_stops_the_motors_and_one_for_another_console_pad_is_left()
    {
        var player = new RumblePlayer(_motors);
        player.Play(Report(0, 1f, 1f, 1000), DualSenseSlot, Start);

        player.Play(Report(1, 1f, 1f, 1000), DualSenseSlot, Start);
        Assert.Equal((DualSenseSlot, 1f, 1f), _motors.Last);

        player.Play(Report(0, 0f, 0f, 0), DualSenseSlot, Start);
        Assert.Equal((DualSenseSlot, 0f, 0f), _motors.Last);
    }

    [Fact]
    public void Picking_up_another_pad_stops_the_first_and_a_report_without_duration_ends_on_its_own()
    {
        var player = new RumblePlayer(_motors);
        player.Play(Report(0, 1f, 0f, 0), DualSenseSlot, Start);

        player.Tick(0, Start);
        Assert.Equal((DualSenseSlot, 0f, 0f), _motors.Last);

        player.Play(Report(0, 1f, 0f, 0), 0, Start);
        player.Tick(0, Start + RumblePlayer.LongestUntimed);
        Assert.Equal((0, 0f, 0f), _motors.Last);
    }

    [Fact]
    public void Without_motors_or_a_played_pad_nothing_runs()
    {
        var player = new RumblePlayer(null);
        player.Play(Report(0, 1f, 1f, 100), DualSenseSlot, Start);
        Assert.False(player.Running);

        var withMotors = new RumblePlayer(_motors);
        withMotors.Play(Report(0, 1f, 1f, 100), -1, Start);
        Assert.False(withMotors.Running);
        Assert.Equal(0, _motors.Calls);
    }

    [Fact]
    public void Trigger_rumble_is_felt_on_the_main_motors_instead_of_stopping_them()
    {
        var player = new RumblePlayer(_motors);
        player.NextGain();
        player.NextGain();

        Assert.Equal(1f, player.Gain);

        player.Play(new RumbleReport(0, 0f, 0.1f, 0.3f, 0.2f, TimeSpan.FromMilliseconds(100)), DualSenseSlot, Start);

        Assert.Equal((DualSenseSlot, 0.3f, 0.2f), _motors.Last);
        Assert.True(player.Running);
    }

    [Fact]
    public void The_gain_steps_through_its_strengths_and_caps_at_full_speed()
    {
        var player = new RumblePlayer(_motors);

        Assert.Equal(2f, player.Gain);
        Assert.Equal(3f, player.NextGain());

        player.Play(Report(0, 0.5f, 0.1f, 100), DualSenseSlot, Start);

        Assert.Equal(1f, _motors.Last.Low);
        Assert.Equal(0.3f, _motors.Last.High, 0.0001f);
    }

    private static RumbleReport Report(byte pad, float low, float high, int milliseconds)
    {
        return new RumbleReport(pad, low, high, 0f, 0f, TimeSpan.FromMilliseconds(milliseconds));
    }

    private sealed class Motors : IGamepadFeedback
    {
        public (int Slot, float Low, float High) Last { get; private set; }

        public int Calls { get; private set; }

        public bool TrySetRumble(int gamepad, float lowFrequency, float highFrequency)
        {
            Last = (gamepad, lowFrequency, highFrequency);
            Calls++;

            return true;
        }

        public bool TrySetLight(int gamepad, float red, float green, float blue)
        {
            return true;
        }
    }
}
