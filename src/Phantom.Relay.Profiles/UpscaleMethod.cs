// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>
/// How the stream's pictures are scaled to a window larger than them (the console sends 1440p at most): through the
/// engine's upscale shader (<see cref="Fsr"/>, <see cref="Xbr"/>), or with one of Lossless Scaling's upscalers on the
/// decoder's GPU path (<see cref="Ls1"/>, <see cref="Ls1Performance"/>, <see cref="Sgsr"/>; see
/// <see cref="UpscaleMethods.RunsOnDecoder"/>). Kept by name in the profiles' JSON.
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

    /// <summary>Lossless Scaling's LS1, from the user's own install, on the decoder's GPU (FSR 1 where it cannot run).</summary>
    Ls1,

    /// <summary>LS1's performance setting: one network step instead of three.</summary>
    Ls1Performance,

    /// <summary>SGSR (Snapdragon Game Super Resolution) as Lossless Scaling ships it, on the decoder's GPU.</summary>
    Sgsr,
}
