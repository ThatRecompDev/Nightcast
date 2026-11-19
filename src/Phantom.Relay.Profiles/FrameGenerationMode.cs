// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>How frames are made between the stream's own (docs/architecture/overview.md#frame-generation).</summary>
public enum FrameGenerationMode
{
    Off,

    /// <summary>Optical flow at a quarter of the stream's resolution.</summary>
    Performance,

    /// <summary>Optical flow at half resolution, checked both ways for occlusion.</summary>
    Quality,
}
