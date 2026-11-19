// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The RT_RCDATA ids of the upscaling shaders in Lossless.dll (the 2025-09 file), found by reflecting every shader
/// outside frame generation's 255 to 302 and reading how each samples and stores (docs/architecture/lossless-upscalers.md),
/// with the shape each must have. Every one is checked against the DLL when an upscaler is built.
/// </summary>
internal static class LosslessUpscaleIds
{
    /// <summary>LS1's first network, its first step: luma of a 3 x 3 neighbourhood to four features, at the picture's size.</summary>
    public const int Ls1Features = 147;

    /// <summary>LS1's second step: a 2 x 2 convolution of the features to four more.</summary>
    public const int Ls1Refine = 148;

    /// <summary>LS1's last step: a 2 x 2 convolution to four luma values, stored as a 2 x 2 block at twice the size.</summary>
    public const int Ls1Detail = 149;

    /// <summary>The ids between one LS1 network and the next (the five sets of three are 147 to 161).</summary>
    public const int Ls1Stride = 3;

    /// <summary>LS1's performance networks, one pass each (141 to 145): 3 x 3 luma to a 2 x 2 block of luma detail.</summary>
    public const int Ls1PerformanceDetail = 141;

    /// <summary>LS1's combine: the detail (sampled at the output's UV) added to the luma of the bilinear picture, chroma kept.</summary>
    public const int Ls1Combine = 146;

    /// <summary>SGSR: one edge-directed pass from the picture to the output.</summary>
    public const int Sgsr = 250;

    // The thread groups: the networks and the combine run 16 x 16, SGSR 8 x 8.
    private const int WideGroup = 16;
    private const int NarrowGroup = 8;
    private const int Colour = 4;
    private const int Luma = 1;

    public static UpscaleShape FeaturesShape { get; } = new(WideGroup, [Colour], [Colour], 0);

    public static UpscaleShape RefineShape { get; } = new(WideGroup, [Colour], [Colour], 0);

    public static UpscaleShape DetailShape { get; } = new(WideGroup, [Colour], [Luma], 0);

    public static UpscaleShape CombineShape { get; } = new(WideGroup, [Luma, Colour], [Colour], 1);

    public static UpscaleShape SgsrShape { get; } = new(NarrowGroup, [Colour], [Colour], 1);

    /// <summary>The id of LS1 network <paramref name="level"/>'s step whose first network's id is <paramref name="first"/>.</summary>
    public static int Ls1(int first, int level)
    {
        return first + (level * Ls1Stride);
    }
}
