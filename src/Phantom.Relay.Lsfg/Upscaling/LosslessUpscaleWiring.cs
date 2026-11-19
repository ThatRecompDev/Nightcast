// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The choices the shaders leave open, made as measured (<c>LosslessUpscalerLiveTests</c>): a shift of where the
/// networks read relative to their thread (<see cref="NetworkShift"/>, texels added to the picture's origin; 0 measured
/// best for both networks) and the format of the features between LS1's steps (<see cref="FeatureFormat"/>; the steps
/// store them biased by 0.5, made for an unsigned 8-bit texture, which also measured best). Other values are for
/// measurements only.
/// </summary>
internal sealed record LosslessUpscaleWiring(int NetworkShift, uint FeatureFormat)
{
    public static LosslessUpscaleWiring Standard { get; } = new(0, Direct3D11Values.Rgba8Format);
}
