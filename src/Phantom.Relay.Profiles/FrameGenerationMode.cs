// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>How frames are made between the stream's own: off, or Lossless Scaling's shader sets (docs/architecture/lsfg.md).</summary>
public enum FrameGenerationMode
{
    Off,

    /// <summary>The lighter shader set: about half the features per pass, a little faster and a little softer.</summary>
    Performance,

    /// <summary>The full shader set.</summary>
    Quality,
}
