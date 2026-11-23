// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App;

/// <summary>Where the platform has no fullscreen of its own: the window stays a window.</summary>
public sealed class WindowedOnly : IWindowMode
{
    public bool Fullscreen => false;

    public void Begin()
    {
    }

    public void Toggle()
    {
    }
}
