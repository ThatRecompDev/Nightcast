// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Where lsfg-vk stands in this process (<see cref="LsfgVkFrameGeneration"/>).</summary>
public enum LsfgVkState
{
    /// <summary>Not loaded, and not wanted when the process started.</summary>
    Off,

    /// <summary>No Lossless Scaling to run it with (or no data folder): it cannot start.</summary>
    Unavailable,

    /// <summary>Being downloaded and unpacked after the player turned it on.</summary>
    Preparing,

    /// <summary>Ready: the process must start again to load it.</summary>
    Restart,

    /// <summary>Loaded in this process with Nightcast's profile: the player's choices apply live.</summary>
    Loaded,

    /// <summary>Its variables were set before the window, yet the layer did not load.</summary>
    NotLoaded,

    /// <summary>The download or the unpacking failed; turning it on tries again.</summary>
    Failed,
}
