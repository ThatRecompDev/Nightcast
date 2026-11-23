// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App;

/// <summary>
/// The window's fullscreen, which the engine's window does not offer yet: the head's platform makes it (borderless over
/// the whole monitor on Windows). <see cref="Begin"/> runs once the window exists and puts it in the mode the head chose.
/// </summary>
public interface IWindowMode
{
    /// <summary>True while the window covers its monitor.</summary>
    bool Fullscreen { get; }

    /// <summary>The window exists: the mode the head chose takes effect.</summary>
    void Begin();

    /// <summary>Fullscreen to windowed and back (F11, Alt+Enter).</summary>
    void Toggle();
}
