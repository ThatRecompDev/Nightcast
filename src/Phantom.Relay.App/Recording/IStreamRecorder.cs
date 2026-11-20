// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Recording;

/// <summary>
/// The head's recorder of the stream (F9): the console's pictures as they came and the pictures shown, kept in files
/// outside the repository. Render thread only; nothing of it waits.
/// </summary>
public interface IStreamRecorder
{
    /// <summary>
    /// F9: starts a recording of the stream as <paramref name="settings"/> show it, or stops the one running (its files
    /// are finished on a thread of their own).
    /// </summary>
    void Toggle(RecordingSettings settings);

    /// <summary>The window closes: a recording running stops, as F9 stops it.</summary>
    void Stop();

    /// <summary>
    /// What the window shows of it now ("● REC 00:42", "saved to …", the comparison's progress), or null for nothing.
    /// The same string until it changes, so the caller compares references; nothing is allocated in between.
    /// </summary>
    string? Status { get; }
}
