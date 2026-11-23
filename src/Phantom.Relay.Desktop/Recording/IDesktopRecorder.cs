// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Recording;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>The desktop's recorder of either platform, with what the head needs once the window closed.</summary>
internal interface IDesktopRecorder : IStreamRecorder
{
    /// <summary>Whether a recording is still being finished.</summary>
    bool IsFinishing { get; }

    /// <summary>Waits until every recording stopped so far is finished.</summary>
    void WaitUntilDone();
}
