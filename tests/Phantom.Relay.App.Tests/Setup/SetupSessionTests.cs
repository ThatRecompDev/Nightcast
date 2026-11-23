// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Setup;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Tools.Probe.Tests.Fakes;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Tests.Setup;

/// <summary>The setup on screen over the probe's fake account and service, stepped as the window's frames step it.</summary>
public sealed class SetupSessionTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly FakeAuth _auth = new() { IsSignedIn = true };
    private readonly FakeSessions _sessions = new();
    private readonly ScreenWriter _screen = new(TextWriter.Null);
    private readonly ScreenConsoleChoice _choice = new();
    private readonly SetupSession _session;

    public SetupSessionTests()
    {
        _session = new SetupSession(new TextSetup(_auth, _sessions, _choice, _screen, null), _screen, _choice);
    }

    public void Dispose()
    {
        _session.Dispose();
        _screen.Dispose();
    }

    [Fact]
    public void The_only_console_is_chosen_without_asking()
    {
        _sessions.Consoles.Add(new XboxConsole("F1", "Living room", "XboxSeriesX", "On", true));
        _session.Start();

        var phase = StepUntilDone();

        Assert.Equal(SetupPhase.Chosen, phase);
        Assert.Equal("F1", _session.Console!.ServerId);
        Assert.Contains(_session.Lines(), static line => line.StartsWith("Signed in.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_sign_in_code_and_its_page_are_on_screen()
    {
        _auth.IsSignedIn = false;
        _sessions.Consoles.Add(new XboxConsole("F1", "Living room", "XboxSeriesX", "On", true));
        _session.Start();

        StepUntilDone();

        Assert.Contains(_session.Lines(), static line => line.Contains("ABCD1234", StringComparison.Ordinal));
        Assert.Contains(_session.Lines(), static line => line.Contains("https://link.example.test/", StringComparison.Ordinal));
    }

    [Fact]
    public void Of_several_consoles_the_one_moved_to_is_chosen()
    {
        AddTwoConsoles();
        _session.Start();
        StepUntilOffered();

        Assert.Contains("> Living room", _session.Lines());
        Assert.Contains("  Bedroom", _session.Lines());
        Assert.Equal(SetupSession.ChoiceLine, _session.Lines()[^1]);

        _session.Step(SetupKeys.Down);
        Assert.Contains("> Bedroom", _session.Lines());
        _session.Step(SetupKeys.Choose);
        var phase = StepUntilDone();

        Assert.Equal(SetupPhase.Chosen, phase);
        Assert.Equal("F2", _session.Console!.ServerId);
    }

    [Fact]
    public void A_choice_cancelled_fails_on_screen_until_b_or_escape_closes_it()
    {
        AddTwoConsoles();
        _session.Start();
        StepUntilOffered();

        _session.Step(SetupKeys.Back);
        var phase = StepUntilDone();

        Assert.Equal(SetupPhase.Failed, phase);
        Assert.Equal(SetupSession.CloseLine, _session.Lines()[^1]);
        Assert.Contains("No such console.", _session.Lines());
        Assert.Equal(SetupPhase.Failed, _session.Step(SetupKeys.Choose));
        Assert.Equal(SetupPhase.Closed, _session.Step(SetupKeys.Back));
    }

    [Fact]
    public void No_console_fails_with_how_to_turn_remote_play_on()
    {
        _session.Start();

        var phase = StepUntilDone();

        Assert.Equal(SetupPhase.Failed, phase);
        Assert.Contains(_session.Lines(), static line => line.StartsWith("No console found.", StringComparison.Ordinal));
    }

    [Fact]
    public void A_network_failure_is_shown_rather_than_thrown()
    {
        using var session = new SetupSession(new FailingSetup(), _screen, _choice);
        session.Start();

        var phase = StepUntilDone(session);

        Assert.Equal(SetupPhase.Failed, phase);
        Assert.Contains("Setup failed: no route to the service", session.Lines());
    }

    [Fact]
    public void B_or_escape_while_it_signs_in_closes_the_window_and_stops_the_sign_in()
    {
        var waiting = new WaitingSetup();
        using var session = new SetupSession(waiting, _screen, _choice);
        session.Start();
        Assert.True(waiting.Started.Wait(Patience));

        var phase = session.Step(SetupKeys.Back);

        Assert.Equal(SetupPhase.Closed, phase);
        Assert.True(waiting.Cancelled.Wait(Patience));
    }

    private void AddTwoConsoles()
    {
        _sessions.Consoles.Add(new XboxConsole("F1", "Living room", "XboxSeriesX", "On", true));
        _sessions.Consoles.Add(new XboxConsole("F2", "Bedroom", "XboxSeriesS", "On", true));
    }

    private SetupPhase StepUntilDone()
    {
        return StepUntilDone(_session);
    }

    private static SetupPhase StepUntilDone(SetupSession session)
    {
        var deadline = DateTime.UtcNow + Patience;
        var phase = session.Step(SetupKeys.None);

        while (phase == SetupPhase.Working && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1);
            phase = session.Step(SetupKeys.None);
        }

        return phase;
    }

    private void StepUntilOffered()
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!_choice.TryOptions(out _, out _) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1);
            _session.Step(SetupKeys.None);
        }
    }
}
