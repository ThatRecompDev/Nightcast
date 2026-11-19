// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>Which of Lossless Scaling's upscalers runs (docs/architecture/lossless-upscalers.md).</summary>
public enum LosslessUpscalerKind
{
    /// <summary>LS1: a small network that adds luma detail at twice the size, three passes, then a bilinear combine to the output.</summary>
    Ls1,

    /// <summary>LS1's performance setting: one network pass instead of three, then the same combine.</summary>
    Ls1Performance,

    /// <summary>SGSR (Snapdragon Game Super Resolution): one edge-directed pass to the output.</summary>
    Sgsr,
}
