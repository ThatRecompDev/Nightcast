// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Profiles.KeyboardMouse;

namespace Phantom.Relay.Profiles.Tests.KeyboardMouse;

public sealed class MouseStickTests
{
    private const float Frame = 1f / 60f;

    private static readonly MouseLookSettings Raw = MouseLookSettings.Standard with { SmoothingFrames = 0f };

    [Fact]
    public void The_curve_never_falls_with_speed_and_never_passes_full_tilt()
    {
        float previous = 0f;

        for (float speed = 0f; speed <= 3f; speed += 0.01f)
        {
            float tilt = MouseStickCurve.Tilt(speed, 0.6f, 0.25f);

            Assert.True(tilt >= previous, $"tilt fell at speed {speed}");
            Assert.InRange(tilt, 0f, 1f);
            previous = tilt;
        }

        Assert.Equal(1f, MouseStickCurve.Tilt(5f, 2f, 0.25f));
        Assert.Equal(0f, MouseStickCurve.Tilt(-1f, 2f, 0.25f));
    }

    [Fact]
    public void The_smallest_move_already_crosses_the_games_dead_zone()
    {
        var stick = new MouseStick(Raw);

        stick.Step(1f, 0f, Frame, aiming: false);

        Assert.True(stick.X > MouseLookSettings.StandardAntiDeadZone);
        Assert.Equal(0f, stick.Y);
    }

    [Fact]
    public void Without_an_anti_dead_zone_a_small_move_stays_small()
    {
        var stick = new MouseStick(Raw with { AntiDeadZone = 0f, Exponent = 1f });

        stick.Step(1f, 0f, Frame, aiming: false);

        Assert.Equal(60f / 2000f, stick.X, 5);
    }

    [Fact]
    public void A_fast_flick_is_clamped_to_the_unit_circle()
    {
        var stick = new MouseStick(Raw);

        stick.Step(5000f, -5000f, Frame, aiming: false);

        float length = MathF.Sqrt((stick.X * stick.X) + (stick.Y * stick.Y));
        Assert.Equal(1f, length, 5);
        Assert.True(stick.X > 0f);
        Assert.True(stick.Y > 0f, "a move away from the player looks up");
    }

    [Fact]
    public void Invert_y_looks_down_when_the_mouse_moves_away()
    {
        var stick = new MouseStick(Raw with { InvertY = true });

        stick.Step(0f, -10f, Frame, aiming: false);

        Assert.True(stick.Y < 0f);
    }

    [Fact]
    public void Separate_sensitivities_scale_each_axis()
    {
        var stick = new MouseStick(Raw with { SensitivityX = 1f, SensitivityY = 0.5f, AntiDeadZone = 0f, Exponent = 1f });

        stick.Step(10f, 10f, Frame, aiming: false);

        Assert.Equal(stick.X / 2f, -stick.Y, 5);
    }

    [Fact]
    public void Aiming_turns_slower()
    {
        var free = new MouseStick(Raw);
        var aiming = new MouseStick(Raw);

        free.Step(20f, 0f, Frame, aiming: false);
        aiming.Step(20f, 0f, Frame, aiming: true);

        Assert.True(aiming.X < free.X);
        Assert.True(aiming.X > MouseLookSettings.StandardAntiDeadZone);
    }

    [Fact]
    public void The_stick_is_back_in_the_centre_within_the_decay_time()
    {
        var stick = new MouseStick(Raw with { AntiDeadZone = 0f });
        const float step = 1f / 240f;

        stick.Step(5000f, 0f, step, aiming: false);
        Assert.Equal(1f, stick.X);
        float elapsed = 0f;

        while (elapsed < MouseLookSettings.StandardDecaySeconds - (step / 2f))
        {
            stick.Step(0f, 0f, step, aiming: false);
            elapsed += step;
        }

        Assert.Equal(0f, stick.X);
        Assert.Equal(0f, stick.Y);
    }

    [Fact]
    public void Decay_is_gradual_not_a_snap()
    {
        var stick = new MouseStick(Raw with { DecaySeconds = 0.1f });

        stick.Step(5000f, 0f, Frame, aiming: false);
        stick.Step(0f, 0f, 0.01f, aiming: false);

        Assert.Equal(0.9f, stick.X, 4);
    }

    [Fact]
    public void Smoothing_moves_part_of_the_way_each_frame()
    {
        var smooth = new MouseStick(MouseLookSettings.Standard with { SmoothingFrames = 1f });

        smooth.Step(5000f, 0f, Frame, aiming: false);

        Assert.Equal(0.5f, smooth.X, 5);
    }

    [Fact]
    public void A_frame_without_time_leaves_the_stick()
    {
        var stick = new MouseStick(Raw);
        stick.Step(100f, 0f, Frame, aiming: false);
        float before = stick.X;

        stick.Step(100f, 0f, 0f, aiming: false);

        Assert.Equal(before, stick.X);
    }

    [Fact]
    public void Smoothing_past_two_frames_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MouseStick(Raw with { SmoothingFrames = 3f }));
    }

    [Fact]
    public void A_step_allocates_nothing()
    {
        var stick = new MouseStick(MouseLookSettings.Standard);
        float delta = 0f;

        var report = AllocationMeter.Measure(() =>
        {
            delta = (delta + 7f) % 50f;
            stick.Step(delta, -delta, Frame, delta > 25f);
        });

        Assert.Equal(0, report.BytesPerRun);
    }
}
