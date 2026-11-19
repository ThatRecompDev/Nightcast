// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles.Input;

/// <summary>Which of the player's hands the console's pad follows (<see cref="PadComposer.Source"/>).</summary>
public enum PadSource
{
    /// <summary>The physical pad; also the start, and the only source when the profile leaves keyboard and mouse out.</summary>
    Controller,

    /// <summary>Keyboard and mouse, from the moment they are used until the controller is.</summary>
    KeyboardMouse,
}
