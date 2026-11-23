// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Setup;

/// <summary>
/// The setup on the window's screen, without the drawing: the flow (<see cref="ISetupFlow"/>, signing in and choosing
/// the console) runs on the thread pool from <see cref="Start"/>, writing its lines to the screen; each frame
/// <see cref="Step"/> takes the player's keys: up and down move through the consoles offered, A or Enter picks one, B
/// or Escape cancels the choice or, while it works, closes the window. It ends <see cref="SetupPhase.Chosen"/> with a
/// console, or <see cref="SetupPhase.Failed"/>, its reason left on screen with how to close it, until B or Escape.
/// </summary>
public sealed class SetupSession : IDisposable
{
    /// <summary>The line the screen ends with once the setup failed.</summary>
    public const string CloseLine = "Press B or Escape to close.";

    /// <summary>The line under the consoles offered.</summary>
    public const string ChoiceLine = "Up and down to pick, A or Enter to choose, B or Escape to cancel.";

    /// <summary>NOT DATA: the newest setup lines the screen shows at most (with the consoles offered under them).</summary>
    public const int ShownLines = 12;

    private const string Marker = "> ";
    private const string Indent = "  ";

    private readonly ISetupFlow _flow;
    private readonly ScreenWriter _screen;
    private readonly ScreenConsoleChoice _choice;
    private readonly CancellationTokenSource _cancel = new();
    private Task<XboxConsole?>? _run;
    private bool _disposed;

    public SetupSession(ISetupFlow flow, ScreenWriter screen, ScreenConsoleChoice choice)
    {
        _flow = flow;
        _screen = screen;
        _choice = choice;
    }

    public SetupPhase Phase { get; private set; }

    /// <summary>The console chosen, once <see cref="Phase"/> is <see cref="SetupPhase.Chosen"/>.</summary>
    public XboxConsole? Console { get; private set; }

    /// <summary>Grows with every change of what <see cref="Lines"/> shows.</summary>
    public int Version => _screen.Version + _choice.Version;

    /// <summary>Starts the flow on the thread pool.</summary>
    public void Start()
    {
        if (_run is not null)
        {
            return;
        }

        _screen.WriteLine("Signing in...");
        var token = _cancel.Token;
        _run = Task.Run(() => _flow.RunAsync(token), token);
    }

    /// <summary>One frame: the player's keys, then the flow's outcome once it has one.</summary>
    public SetupPhase Step(SetupKeys keys)
    {
        if (Phase == SetupPhase.Failed && (keys & SetupKeys.Back) != 0)
        {
            Phase = SetupPhase.Closed;
        }

        if (Phase != SetupPhase.Working)
        {
            return Phase;
        }

        Take(keys);

        if (Phase == SetupPhase.Working && _run is not null && _run.IsCompleted)
        {
            Finish(_run);
        }

        return Phase;
    }

    /// <summary>What the screen shows: the newest setup lines, and the consoles offered while one is to be picked.</summary>
    public string[] Lines()
    {
        var lines = new List<string>(_screen.Lines(ShownLines));

        if (_choice.TryOptions(out var names, out var highlighted))
        {
            lines.Add(string.Empty);

            for (var index = 0; index < names.Length; index++)
            {
                var lead = Indent;

                if (index == highlighted)
                {
                    lead = Marker;
                }

                lines.Add(lead + names[index]);
            }

            lines.Add(string.Empty);
            lines.Add(ChoiceLine);
        }

        return [.. lines];
    }

    /// <summary>Stops the flow (signing in waits on the network).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancel.Cancel();
        _cancel.Dispose();
    }

    private void Take(SetupKeys keys)
    {
        if (_choice.TryOptions(out _, out _))
        {
            Choose(keys);

            return;
        }

        if ((keys & SetupKeys.Back) != 0)
        {
            _cancel.Cancel();
            Phase = SetupPhase.Closed;
        }
    }

    private void Choose(SetupKeys keys)
    {
        if ((keys & SetupKeys.Up) != 0)
        {
            _choice.Move(-1);
        }

        if ((keys & SetupKeys.Down) != 0)
        {
            _choice.Move(1);
        }

        if ((keys & SetupKeys.Back) != 0)
        {
            _choice.Cancel();
        }
        else if ((keys & SetupKeys.Choose) != 0)
        {
            _choice.Pick();
        }
    }

    /// <summary>The flow's outcome: a console, or why there is none, with how to close the window.</summary>
    private void Finish(Task<XboxConsole?> run)
    {
        if (run.IsCompletedSuccessfully && run.Result is not null)
        {
            Console = run.Result;
            Phase = SetupPhase.Chosen;

            return;
        }

        if (run.IsFaulted)
        {
            _screen.WriteLine($"Setup failed: {run.Exception.GetBaseException().Message}");
        }

        _screen.WriteLine();
        _screen.WriteLine(CloseLine);
        Phase = SetupPhase.Failed;
    }
}
