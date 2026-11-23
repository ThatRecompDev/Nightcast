// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// What the loader said: whether it lists the layer (its manifest found and not switched off), the version the layer
/// reported (null: it did not load or took no profile), and when neither, why the check itself failed (no loader).
/// </summary>
public sealed record LayerCheck(bool Listed, string? Version, string? Problem)
{
    /// <summary>The layer runs in this process: it loaded and took Nightcast's profile.</summary>
    public bool Loaded => Version is not null;
}
