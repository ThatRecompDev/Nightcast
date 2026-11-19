// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Xbox.Tests.Fakes;

/// <summary>Records each reported state at once (the BCL's Progress posts to a context, too late for an assert).</summary>
public sealed class SynchronousProgress : IProgress<SessionState>
{
    private readonly List<SessionState> _states;

    public SynchronousProgress(List<SessionState> states)
    {
        _states = states;
    }

    public void Report(SessionState value)
    {
        _states.Add(value);
    }
}
