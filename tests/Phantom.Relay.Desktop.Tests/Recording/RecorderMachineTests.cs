// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class RecorderMachineTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 21, 14, 3, TimeSpan.Zero));

    [Fact]
    public void Idle_shows_nothing_and_a_start_waits_for_a_key_frame()
    {
        var machine = new RecorderMachine(_clock);
        var idle = machine.Status();

        var started = machine.TryStart();

        Assert.Null(idle);
        Assert.True(started);
        Assert.Equal(RecorderPhase.WaitingForKeyFrame, machine.Phase);
        Assert.Equal(RecorderMachine.WaitingText, machine.Status());
    }

    [Fact]
    public void Recording_counts_seconds_from_the_key_frame()
    {
        var machine = new RecorderMachine(_clock);
        _ = machine.TryStart();
        _clock.Advance(TimeSpan.FromSeconds(3));
        machine.KeyFrame(_clock.GetTimestamp());

        _clock.Advance(TimeSpan.FromSeconds(42.6));

        Assert.Equal(RecorderPhase.Recording, machine.Phase);
        Assert.Equal("● REC 00:42", machine.Status());
    }

    [Fact]
    public void The_text_is_the_same_string_until_a_second_passes()
    {
        var machine = new RecorderMachine(_clock);
        _ = machine.TryStart();
        machine.KeyFrame(_clock.GetTimestamp());
        var first = machine.Status();

        _clock.Advance(TimeSpan.FromMilliseconds(400));
        var same = machine.Status();
        _clock.Advance(TimeSpan.FromMilliseconds(700));
        var next = machine.Status();

        Assert.Same(first, same);
        Assert.Equal("● REC 00:01", next);
    }

    [Fact]
    public void Minutes_go_past_an_hour()
    {
        Assert.Equal("● REC 75:03", RecorderMachine.RecordingText(4503));
    }

    [Fact]
    public void A_stop_saves_then_the_result_shows_for_a_while()
    {
        var machine = new RecorderMachine(_clock);
        _ = machine.TryStart();
        Assert.True(machine.TryStop());
        var saving = machine.Status();

        machine.Saved("saved to here");
        var saved = machine.Status();
        _clock.Advance(RecorderMachine.ResultShownFor);

        Assert.Equal(RecorderMachine.SavingText, saving);
        Assert.Equal("saved to here", saved);
        Assert.Equal(RecorderPhase.Idle, machine.Phase);
        Assert.Null(machine.Status());
    }

    [Fact]
    public void The_comparison_shows_its_progress_however_long_it_takes_then_its_result()
    {
        var machine = new RecorderMachine(_clock);
        _ = machine.TryStart();
        _ = machine.TryStop();
        machine.Saved("saved to here");

        machine.Comparing(42);
        _clock.Advance(TimeSpan.FromMinutes(2));
        var comparing = machine.Status();
        machine.Compared("saved to here | compare.mp4 made");

        Assert.Equal("saved to here | compare 42 %", comparing);
        Assert.Equal("saved to here | compare.mp4 made", machine.Status());
    }

    [Fact]
    public void Starting_twice_or_stopping_while_idle_or_saving_is_refused()
    {
        var machine = new RecorderMachine(_clock);
        var idleStop = machine.TryStop();
        _ = machine.TryStart();
        var secondStart = machine.TryStart();
        _ = machine.TryStop();

        var savingStart = machine.TryStart();
        var savingStop = machine.TryStop();

        Assert.False(idleStop);
        Assert.False(secondStart);
        Assert.False(savingStart);
        Assert.False(savingStop);
        Assert.Equal(RecorderPhase.Saving, machine.Phase);
    }

    [Fact]
    public void A_new_recording_shows_over_an_earlier_comparison()
    {
        var machine = new RecorderMachine(_clock);
        _ = machine.TryStart();
        _ = machine.TryStop();
        machine.Saved("saved to first");
        machine.Comparing(10);

        _ = machine.TryStart();

        Assert.Equal(RecorderMachine.WaitingText, machine.Status());
    }

    [Fact]
    public void A_refused_start_says_why_for_a_while_and_stays_idle()
    {
        var machine = new RecorderMachine(_clock);

        machine.Refused("nothing to record yet");

        Assert.Equal(RecorderPhase.Idle, machine.Phase);
        Assert.Equal("nothing to record yet", machine.Status());
    }
}
