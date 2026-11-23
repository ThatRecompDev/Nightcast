// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Setup;

/// <summary>
/// The console picked on the window's setup screen: the setup asks (<see cref="ChooseAsync"/>, from its thread) and
/// waits; the render thread shows the names with the highlighted one (<see cref="TryOptions"/>), moves the highlight,
/// and picks it or cancels, which ends the wait. A wait cancelled from outside ends as no choice.
/// </summary>
public sealed class ScreenConsoleChoice : IConsoleChoice
{
    private readonly Lock _gate = new();
    private string[]? _names;
    private int _highlighted;
    private TaskCompletionSource<int?>? _pending;
    private int _version;

    /// <summary>Grows with every change of the names or the highlight, so a reader lays them out again only after one.</summary>
    public int Version => Volatile.Read(ref _version);

    public Task<int?> ChooseAsync(IReadOnlyList<XboxConsole> consoles, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(consoles);

        var pending = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            _names = [.. consoles.Select(static console => console.Name)];
            _highlighted = 0;
            _pending = pending;
            _version++;
        }

        cancellation.Register(Cancel);

        return pending.Task;
    }

    /// <summary>The names offered and the highlighted one's index; false while nothing is asked.</summary>
    public bool TryOptions(out string[] names, out int highlighted)
    {
        lock (_gate)
        {
            names = _names ?? [];
            highlighted = _highlighted;

            return _names is not null;
        }
    }

    /// <summary>Moves the highlight by <paramref name="step"/> names, wrapping around.</summary>
    public void Move(int step)
    {
        lock (_gate)
        {
            if (_names is null || _names.Length == 0)
            {
                return;
            }

            _highlighted = (((_highlighted + step) % _names.Length) + _names.Length) % _names.Length;
            _version++;
        }
    }

    /// <summary>Picks the highlighted console: the setup goes on with it.</summary>
    public void Pick()
    {
        Finish(pick: true);
    }

    /// <summary>Picks none: the setup ends without a console.</summary>
    public void Cancel()
    {
        Finish(pick: false);
    }

    private void Finish(bool pick)
    {
        TaskCompletionSource<int?>? pending;
        int? chosen = null;

        lock (_gate)
        {
            pending = _pending;

            if (pick)
            {
                chosen = _highlighted;
            }

            _pending = null;
            _names = null;
            _version++;
        }

        pending?.TrySetResult(chosen);
    }
}
