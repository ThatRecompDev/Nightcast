// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>How the stream's pictures are scaled to the display, through the engine's picture upscaler.</summary>
public enum UpscaleMethod
{
    Bilinear,

    /// <summary>The engine's sharp-bilinear mode.</summary>
    Sharp,
}
