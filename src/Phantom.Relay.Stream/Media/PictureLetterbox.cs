// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Where the stream's 16:9 picture sits in a window of another shape, as the renderers take a rectangle: left, top,
/// right and bottom, as fractions of the window. With <see cref="PictureFit.Bars"/> it is centred and as large as fits;
/// with <see cref="PictureFit.Stretch"/> it is the whole window.
/// </summary>
public static class PictureLetterbox
{
    /// <summary>The console streams 16:9 at every tier.</summary>
    public const float StreamAspect = 16f / 9f;

    private const float Half = 0.5f;

    private static readonly Vector4 WholeWindow = new(0f, 0f, 1f, 1f);

    /// <summary>The picture's rectangle with bars: left, top, right, bottom.</summary>
    public static Vector4 Of(int windowWidth, int windowHeight)
    {
        return Of(windowWidth, windowHeight, PictureFit.Bars);
    }

    /// <summary>The picture's rectangle under <paramref name="fit"/>: left, top, right, bottom.</summary>
    public static Vector4 Of(int windowWidth, int windowHeight, PictureFit fit)
    {
        if (fit == PictureFit.Stretch)
        {
            return WholeWindow;
        }

        float aspect = (float)windowWidth / Math.Max(1, windowHeight);

        if (aspect > StreamAspect)
        {
            float width = StreamAspect / aspect;
            float left = (1f - width) * Half;

            return new Vector4(left, 0f, left + width, 1f);
        }

        float height = aspect / StreamAspect;
        float top = (1f - height) * Half;

        return new Vector4(0f, top, 1f, top + height);
    }
}
