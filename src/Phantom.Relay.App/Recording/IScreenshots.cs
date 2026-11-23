// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Recording;

/// <summary>
/// The head's screenshots (F1, the menu): the console's own picture saved as a PNG outside the repository. Render
/// thread only for <see cref="Take"/>; nothing of it waits.
/// </summary>
public interface IScreenshots
{
    /// <summary>Saves the next decoded picture; the encoding and writing run on a thread of their own.</summary>
    void Take();

    /// <summary>
    /// What the window shows of the screenshots now ("Screenshot saved: …", or why none was), or null for nothing. The
    /// same string until it changes, so the caller compares references.
    /// </summary>
    string? Status { get; }
}
