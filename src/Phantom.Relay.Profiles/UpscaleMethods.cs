// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>Where each <see cref="UpscaleMethod"/> runs, and the name the stats give it.</summary>
public static class UpscaleMethods
{
    /// <summary>
    /// Whether the method runs where the pictures are decoded (Lossless Scaling's upscalers, on the decoder's GPU path),
    /// which then hands over pictures already at the window's size; the others run in the renderer.
    /// </summary>
    public static bool RunsOnDecoder(this UpscaleMethod method)
    {
        return method is UpscaleMethod.Ls1 or UpscaleMethod.Ls1Performance or UpscaleMethod.Sgsr;
    }

    /// <summary>"FSR 1", "LS1", "LS1 performance", "SGSR", "xBR", or "upscale off".</summary>
    public static string Label(this UpscaleMethod method)
    {
        return method switch
        {
            UpscaleMethod.Bilinear => "upscale off",
            UpscaleMethod.Xbr => "xBR",
            UpscaleMethod.Ls1 => "LS1",
            UpscaleMethod.Ls1Performance => "LS1 performance",
            UpscaleMethod.Sgsr => "SGSR",
            _ => "FSR 1",
        };
    }
}
