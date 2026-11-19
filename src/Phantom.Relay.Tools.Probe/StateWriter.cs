// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>Writes each session state when it changes.</summary>
public sealed class StateWriter : IProgress<SessionState>
{
    private readonly TextWriter _output;
    private SessionState? _last;

    public StateWriter(TextWriter output)
    {
        _output = output;
    }

    public void Report(SessionState value)
    {
        if (_last == value)
        {
            return;
        }

        _last = value;
        _output.WriteLine($"  session: {value}");
    }
}
