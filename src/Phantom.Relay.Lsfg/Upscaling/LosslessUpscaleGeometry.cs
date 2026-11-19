// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The sizes an upscaler works at: the picture in (<see cref="InputWidth"/> x <see cref="InputHeight"/>, its
/// texture's size too), the picture out, and the output texture, which may be larger (an NV12 slot's even size); the
/// picture out fills its top-left corner.
/// </summary>
public sealed record LosslessUpscaleGeometry(int InputWidth, int InputHeight, int OutputWidth, int OutputHeight, int OutputTextureWidth, int OutputTextureHeight)
{
    /// <summary>An output texture exactly the output's size.</summary>
    public static LosslessUpscaleGeometry Exact(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
    {
        return new LosslessUpscaleGeometry(inputWidth, inputHeight, outputWidth, outputHeight, outputWidth, outputHeight);
    }

    /// <summary>Whether every size is positive, the texture holds the output and the output is no smaller than the input.</summary>
    public bool IsValid => InputWidth > 0 && InputHeight > 0 && OutputWidth >= InputWidth && OutputHeight >= InputHeight
        && OutputTextureWidth >= OutputWidth && OutputTextureHeight >= OutputHeight;
}
