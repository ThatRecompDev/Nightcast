// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Recording;

/// <summary>Where the recorder is: idle, recording (waiting for its first key frame, then writing) or saving.</summary>
internal enum RecorderPhase
{
    /// <summary>Nothing records; F9 starts a recording.</summary>
    Idle,

    /// <summary>F9 was pressed; the files start at the first key frame, asked for at once.</summary>
    WaitingForKeyFrame,

    /// <summary>Both files are written.</summary>
    Recording,

    /// <summary>F9 stopped it; the files are being finished on a thread of their own.</summary>
    Saving,
}
