// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Stills;

/// <summary>A decoded picture as a PNG file: converted to RGB (<see cref="PictureRgb"/>), then written (<see cref="PngWriter"/>).</summary>
public static class StillImage
{
    /// <summary>Writes <paramref name="picture"/> as a PNG to <paramref name="output"/>.</summary>
    public static void WritePng(VideoFrame picture, System.IO.Stream output)
    {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(output);

        var rgb = new byte[PictureRgb.Size(picture)];
        PictureRgb.Convert(picture, rgb);
        PngWriter.Write(output, picture.Width, picture.Height, rgb);
    }
}
