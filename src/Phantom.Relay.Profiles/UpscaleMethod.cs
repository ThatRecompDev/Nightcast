// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>
/// How the stream's pictures are scaled to a window larger than them (the console sends 1440p at most), through the
/// engine's upscale shader. Kept by name in the profiles' JSON.
/// </summary>
public enum UpscaleMethod
{
    /// <summary>Upscaling off: the picture sampled bilinearly into the window.</summary>
    Bilinear,

    /// <summary>The name profiles kept before FSR 1 came: it now means <see cref="Fsr"/>.</summary>
    Sharp,

    /// <summary>AMD's FSR 1: EASU to the window's size, then RCAS sharpening. The default.</summary>
    Fsr,

    /// <summary>xBR edge-directed scaling (made for pixel art; offered to compare).</summary>
    Xbr,
}
