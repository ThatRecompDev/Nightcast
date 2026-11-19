// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;

namespace Phantom.Relay.Stream.Media;

/// <summary>Where the stream's 16:9 picture sits in a window of another shape: centred, as large as fits.</summary>
public static class PictureLetterbox
{
    /// <summary>The console streams 16:9 at every tier.</summary>
    public const float StreamAspect = 16f / 9f;

    private const float Half = 0.5f;

    /// <summary>The picture's rectangle as fractions of the window: x, y, width, height.</summary>
    public static Vector4 Of(int windowWidth, int windowHeight)
    {
        float aspect = (float)windowWidth / Math.Max(1, windowHeight);

        if (aspect > StreamAspect)
        {
            float width = StreamAspect / aspect;

            return new Vector4((1f - width) * Half, 0f, width, 1f);
        }

        float height = aspect / StreamAspect;

        return new Vector4(0f, (1f - height) * Half, 1f, height);
    }
}
