// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// What the renderer asks of a decoder that can upscale its pictures where they are decoded: the method (one that
/// <see cref="UpscaleMethods.RunsOnDecoder"/>), the size the window shows the picture at, so the renderer draws it one
/// to one, and the method's strength (LS1's network, <see cref="MinimumStrength"/> to <see cref="MaximumStrength"/>,
/// more detail as it rises). <see cref="None"/> asks for the pictures at their own size.
/// </summary>
public sealed record PictureScale(UpscaleMethod Method, int Width, int Height, int Strength = PictureScale.DefaultStrength)
{
    public const int MinimumStrength = 0;
    public const int MaximumStrength = 4;

    /// <summary>LS1's middle network: on clean pictures its edges measured as strong as the truth's.</summary>
    public const int DefaultStrength = 2;

    public static PictureScale None { get; } = new(UpscaleMethod.Bilinear, 0, 0);

    /// <summary>Whether the decoder is asked to upscale at all.</summary>
    public bool IsOn => Method.RunsOnDecoder() && Width > 0 && Height > 0;

    /// <summary>F7's order with LS1: 2, 3, 4 (most detail), 0, 1, then 2 again.</summary>
    public static int NextStrength(int strength)
    {
        if (strength >= MaximumStrength)
        {
            return MinimumStrength;
        }

        return Math.Max(MinimumStrength, strength + 1);
    }
}