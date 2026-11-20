// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// The third-person case the frame generators are scored on: a still, textured foreground (an ellipse the size of a
/// character's head and shoulders at 1440p) over a textured background that moves sideways, as when the camera turns
/// around a character. A generated middle frame is compared with the ideal one in three regions: the band around the
/// foreground's edge (where the background is uncovered and covered), the foreground's inside and the background away
/// from it. Shared by LSFG's harness and NVIDIA FRUC's (linked into its tests), so both are scored alike.
/// </summary>
internal static class ArtifactScene
{
    public const int Width = 2560;
    public const int Height = 1440;
    public const int CropSize = 800;

    private const int CentreX = 1280;
    private const int CentreY = 760;
    private const int RadiusX = 200;
    private const int RadiusY = 270;
    private const int MinimumBand = 32;
    private const int DifferenceGain = 4;

    /// <summary>A slow, a moderate and a fast camera turn at 30 fps, in pixels a frame.</summary>
    public static readonly int[] Shifts = [16, 32, 64];

    /// <summary>The background (value noise in 24-pixel cells on a gradient, moved right by shift) with the still foreground over it.</summary>
    public static byte[] Scene(int shift)
    {
        var pixels = new byte[Width * Height * 4];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = ((y * Width) + x) * 4;
                Paint(pixels.AsSpan(offset, 4), x, y, shift);
            }
        }

        return pixels;
    }

    /// <summary>The ideal frame between the scenes at <paramref name="shift"/> and twice it: the background at 1.5 times.</summary>
    public static byte[] Ideal(int shift)
    {
        return Scene(shift * 3 / 2);
    }

    public static byte[] Blend(byte[] first, byte[] second)
    {
        var blend = new byte[first.Length];

        for (var index = 0; index < first.Length; index++)
        {
            blend[index] = (byte)((first[index] + second[index] + 1) / 2);
        }

        return blend;
    }

    /// <summary>The mean luma difference in the edge band, inside the foreground, in the background beyond the band, and overall.</summary>
    public static (double Edge, double Inside, double Outside, double All) Errors(byte[] frame, byte[] ideal, int shift)
    {
        var sums = new double[3];
        var counts = new long[3];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = ((y * Width) + x) * 4;
                var region = Region(Distance(x, y), shift);
                sums[region] += Math.Abs(Luma(frame, offset) - Luma(ideal, offset));
                counts[region]++;
            }
        }

        return (sums[0] / counts[0], sums[1] / counts[1], sums[2] / counts[2], sums.Sum() / counts.Sum());
    }

    /// <summary>Each pixel's luma difference, gained, on black (red to yellow).</summary>
    public static byte[] Difference(byte[] frame, byte[] ideal)
    {
        var difference = new byte[frame.Length];

        for (var offset = 0; offset < frame.Length; offset += 4)
        {
            var gained = Math.Abs(Luma(frame, offset) - Luma(ideal, offset)) * DifferenceGain;
            difference[offset] = (byte)Math.Min(255, gained);
            difference[offset + 1] = (byte)Math.Min(255, gained / 2);
            difference[offset + 3] = 255;
        }

        return difference;
    }

    /// <summary>The <see cref="CropSize"/> square around the foreground.</summary>
    public static byte[] Crop(byte[] pixels)
    {
        var crop = new byte[CropSize * CropSize * 4];
        var left = CentreX - (CropSize / 2);
        var top = CentreY - (CropSize / 2);

        for (var row = 0; row < CropSize; row++)
        {
            pixels.AsSpan((((top + row) * Width) + left) * 4, CropSize * 4).CopyTo(crop.AsSpan(row * CropSize * 4));
        }

        return crop;
    }

    private static void Paint(Span<byte> pixel, int x, int y, int shift)
    {
        pixel[3] = 255;

        if (Distance(x, y) <= 0)
        {
            // Skin-like, with a finer texture of its own: 8-pixel cells.
            var skin = 120 + (Noise(x, y, 8) * 0.4);
            pixel[0] = (byte)Math.Clamp(skin + 40, 0, 255);
            pixel[1] = (byte)Math.Clamp(skin, 0, 255);
            pixel[2] = (byte)Math.Clamp(skin - 30, 0, 255);

            return;
        }

        var source = x - shift;
        var value = 30 + (source * 60 / Width) + (Noise(source, y, 24) * 0.8);
        pixel[0] = (byte)Math.Clamp(value * 0.6, 0, 255);
        pixel[1] = (byte)Math.Clamp(value, 0, 255);
        pixel[2] = (byte)Math.Clamp(value * 0.8, 0, 255);
    }

    // About how far outside the foreground's edge a point is, in pixels (negative inside).
    private static double Distance(int x, int y)
    {
        var u = (x - CentreX) / (double)RadiusX;
        var v = (y - CentreY) / (double)RadiusY;

        return (Math.Sqrt((u * u) + (v * v)) - 1) * Math.Min(RadiusX, RadiusY);
    }

    // Bilinear value noise over a hashed grid of cells, 0 to 190.
    private static double Noise(int x, int y, double cell)
    {
        var cellX = (int)Math.Floor(x / cell);
        var cellY = (int)Math.Floor(y / cell);
        var u = (x / cell) - cellX;
        var v = (y / cell) - cellY;

        var top = (Hash(cellX, cellY) * (1 - u)) + (Hash(cellX + 1, cellY) * u);
        var bottom = (Hash(cellX, cellY + 1) * (1 - u)) + (Hash(cellX + 1, cellY + 1) * u);

        return (top * (1 - v)) + (bottom * v);
    }

    private static double Hash(int x, int y)
    {
        var hash = (uint)((x * 73856093) ^ (y * 19349663));
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;

        return hash % 190;
    }

    // The edge band reaches as far as the background moves in two frames, at least 32 pixels.
    private static int Region(double distance, int shift)
    {
        if (Math.Abs(distance) <= Math.Max(MinimumBand, 2 * shift))
        {
            return 0;
        }

        if (distance < 0)
        {
            return 1;
        }

        return 2;
    }

    private static double Luma(byte[] pixels, int offset)
    {
        return (pixels[offset] * 0.299) + (pixels[offset + 1] * 0.587) + (pixels[offset + 2] * 0.114);
    }
}
