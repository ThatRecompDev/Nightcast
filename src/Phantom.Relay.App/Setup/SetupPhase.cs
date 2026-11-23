// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Setup;

/// <summary>Where the setup screen is.</summary>
public enum SetupPhase
{
    /// <summary>Signing in or listing the consoles, or waiting for the player to pick one.</summary>
    Working,

    /// <summary>A console was chosen: the stream takes the window over.</summary>
    Chosen,

    /// <summary>The setup ended without a console; its reason stays on screen until B or Escape.</summary>
    Failed,

    /// <summary>The player closed the setup: the window closes.</summary>
    Closed,
}
