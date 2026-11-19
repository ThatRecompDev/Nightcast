// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// Test pictures for the upscalers, drawn from continuous functions so a 2160p "truth" and a 1440p picture of the
/// same scene come from one source (each pixel the average of the function over its footprint, as a camera or a
/// game's downscale would): a sharp test chart (wedges at every angle, small glyphs, a zone plate, thin lines and
/// checkerboards) and a natural-looking texture (fractal noise, grass-like detail, shaded pebbles). The 1440p picture
/// comes clean, softened (a 3 x 3 binomial blur, as an encoder's smoothing) or blocky (8 x 8 DCT quantisation, as a
/// starved encoder's). Shared with the app's tests (linked), which measure FSR 1 on the same pictures.
/// RGBA bytes, rows top first.
/// </summary>
public static class UpscaleTestImages
{
    public const int TruthWidth = 3840;
    public const int TruthHeight = 2160;
    public const int PictureWidth = 2560;
    public const int PictureHeight = 1440;

    // JPEG's quantisation tables (ITU-T T.81 Annex K), luma and chroma, for the blocky variant.
    private static readonly int[] LumaTable =
    [
        16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57, 69, 56, 14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92, 49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99,
    ];

    private static readonly int[] ChromaTable =
    [
        17, 18, 24, 47, 99, 99, 99, 99, 18, 21, 26, 66, 99, 99, 99, 99, 24, 26, 56, 99, 99, 99, 99, 99, 47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
    ];

    public enum Scene
    {
        Chart,
        Natural,
    }

    public enum Variant
    {
        Clean,
        Soft,
        Blocky,
    }

    /// <summary>The scene at <paramref name="width"/> x <paramref name="height"/> (pixels averaged over their footprint in truth units).</summary>
    public static byte[] Render(Scene scene, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var scale = TruthWidth / (double)width;
        const int Samples = 3;

        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                double r = 0, g = 0, b = 0;

                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var u = (x + ((sx + 0.5) / Samples)) * scale;
                        var v = (y + ((sy + 0.5) / Samples)) * scale;
                        var (cr, cg, cb) = scene == Scene.Chart ? Chart(u, v) : Natural(u, v);
                        r += cr;
                        g += cg;
                        b += cb;
                    }
                }

                const double Count = Samples * Samples;
                var offset = ((y * width) + x) * 4;
                pixels[offset] = ToByte(r / Count);
                pixels[offset + 1] = ToByte(g / Count);
                pixels[offset + 2] = ToByte(b / Count);
                pixels[offset + 3] = 255;
            }
        });

        return pixels;
    }

    /// <summary>The 1440p picture of <paramref name="scene"/> as <paramref name="variant"/> makes it.</summary>
    public static byte[] Picture(Scene scene, Variant variant)
    {
        var clean = Render(scene, PictureWidth, PictureHeight);

        return variant switch
        {
            Variant.Soft => Soften(clean, PictureWidth, PictureHeight),
            Variant.Blocky => Blocky(clean, PictureWidth, PictureHeight, 55),
            _ => clean,
        };
    }

    /// <summary>A 3 x 3 binomial blur ([1 2 1] each way), edges clamped.</summary>
    public static byte[] Soften(byte[] rgba, int width, int height)
    {
        var result = new byte[rgba.Length];

        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var sum = 0;

                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var weight = (2 - Math.Abs(dx)) * (2 - Math.Abs(dy));
                            sum += weight * rgba[(((Math.Clamp(y + dy, 0, height - 1) * width) + Math.Clamp(x + dx, 0, width - 1)) * 4) + c];
                        }
                    }

                    result[(((y * width) + x) * 4) + c] = (byte)((sum + 8) / 16);
                }

                result[(((y * width) + x) * 4) + 3] = 255;
            }
        });

        return result;
    }

    /// <summary>JPEG-like: YCbCr (BT.601 full range), each 8 x 8 block's DCT quantised at <paramref name="quality"/> (1 to 100), back to RGB.</summary>
    public static byte[] Blocky(byte[] rgba, int width, int height, int quality)
    {
        var scale = quality < 50 ? 5000.0 / quality : 200.0 - (2 * quality);
        var planes = new double[3][];

        for (var c = 0; c < 3; c++)
        {
            planes[c] = new double[width * height];
        }

        for (var index = 0; index < width * height; index++)
        {
            double r = rgba[index * 4], g = rgba[(index * 4) + 1], b = rgba[(index * 4) + 2];
            planes[0][index] = (0.299 * r) + (0.587 * g) + (0.114 * b) - 128;
            planes[1][index] = (-0.168736 * r) - (0.331264 * g) + (0.5 * b);
            planes[2][index] = (0.5 * r) - (0.418688 * g) - (0.081312 * b);
        }

        for (var c = 0; c < 3; c++)
        {
            var table = c == 0 ? LumaTable : ChromaTable;
            var plane = planes[c];
            Parallel.For(0, height / 8, by => QuantiseRow(plane, width, by, table, scale));
        }

        var result = new byte[rgba.Length];

        for (var index = 0; index < width * height; index++)
        {
            var luma = planes[0][index] + 128;
            var cb = planes[1][index];
            var cr = planes[2][index];

            result[index * 4] = ToByte((luma + (1.402 * cr)) / 255);
            result[(index * 4) + 1] = ToByte((luma - (0.344136 * cb) - (0.714136 * cr)) / 255);
            result[(index * 4) + 2] = ToByte((luma + (1.772 * cb)) / 255);
            result[(index * 4) + 3] = 255;
        }

        return result;
    }

    /// <summary>
    /// The picture as the decoder hands it: NV12 (luma, then interleaved Cb Cr at half size), BT.709 limited range,
    /// chroma averaged over each 2 x 2 block.
    /// </summary>
    public static byte[] ToNv12(byte[] rgba, int width, int height)
    {
        var (luma, cb, cr) = ToYuv420(rgba, width, height);
        var planes = new byte[width * height * 3 / 2];
        luma.CopyTo(planes, 0);

        for (var index = 0; index < cb.Length; index++)
        {
            planes[(width * height) + (index * 2)] = cb[index];
            planes[(width * height) + (index * 2) + 1] = cr[index];
        }

        return planes;
    }

    /// <summary>The picture as three planes (Y, then Cb and Cr at half size), BT.709 limited range.</summary>
    public static (byte[] Luma, byte[] Cb, byte[] Cr) ToYuv420(byte[] rgba, int width, int height)
    {
        var luma = new byte[width * height];
        var cb = new byte[width * height / 4];
        var cr = new byte[width * height / 4];

        for (var index = 0; index < width * height; index++)
        {
            double r = rgba[index * 4] / 255.0, g = rgba[(index * 4) + 1] / 255.0, b = rgba[(index * 4) + 2] / 255.0;
            luma[index] = (byte)Math.Clamp(Math.Round(16 + (219 * ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)))), 0, 255);
        }

        for (var y = 0; y < height / 2; y++)
        {
            for (var x = 0; x < width / 2; x++)
            {
                double sumB = 0, sumR = 0;

                for (var corner = 0; corner < 4; corner++)
                {
                    var index = (((2 * y) + (corner / 2)) * width) + (2 * x) + (corner % 2);
                    double r = rgba[index * 4] / 255.0, g = rgba[(index * 4) + 1] / 255.0, b = rgba[(index * 4) + 2] / 255.0;
                    var yy = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                    sumB += (b - yy) / 1.8556;
                    sumR += (r - yy) / 1.5748;
                }

                cb[(y * (width / 2)) + x] = (byte)Math.Clamp(Math.Round(128 + (224 * sumB / 4)), 0, 255);
                cr[(y * (width / 2)) + x] = (byte)Math.Clamp(Math.Round(128 + (224 * sumR / 4)), 0, 255);
            }
        }

        return (luma, cb, cr);
    }

    /// <summary>A half-pixel-centred bilinear resize with clamped edges (what sampling the picture into a larger rectangle gives).</summary>
    public static byte[] Bilinear(byte[] rgba, int width, int height, int outWidth, int outHeight)
    {
        var result = new byte[outWidth * outHeight * 4];

        Parallel.For(0, outHeight, y =>
        {
            var v = Math.Clamp(((y + 0.5) * height / outHeight) - 0.5, 0, height - 1);
            var y0 = (int)v;
            var y1 = Math.Min(y0 + 1, height - 1);
            var fy = v - y0;

            for (var x = 0; x < outWidth; x++)
            {
                var u = Math.Clamp(((x + 0.5) * width / outWidth) - 0.5, 0, width - 1);
                var x0 = (int)u;
                var x1 = Math.Min(x0 + 1, width - 1);
                var fx = u - x0;

                for (var c = 0; c < 4; c++)
                {
                    var top = (rgba[(((y0 * width) + x0) * 4) + c] * (1 - fx)) + (rgba[(((y0 * width) + x1) * 4) + c] * fx);
                    var bottom = (rgba[(((y1 * width) + x0) * 4) + c] * (1 - fx)) + (rgba[(((y1 * width) + x1) * 4) + c] * fx);
                    result[(((y * outWidth) + x) * 4) + c] = (byte)Math.Round((top * (1 - fy)) + (bottom * fy));
                }
            }
        });

        return result;
    }

    /// <summary>Luma PSNR (BT.709 weights) against the truth, a border of <paramref name="border"/> pixels left out.</summary>
    public static double LumaPsnr(byte[] image, byte[] truth, int width, int height, int border)
    {
        double sum = 0;
        long count = 0;

        for (var y = border; y < height - border; y++)
        {
            for (var x = border; x < width - border; x++)
            {
                var index = ((y * width) + x) * 4;
                var difference = Luma(image, index) - Luma(truth, index);
                sum += difference * difference;
                count++;
            }
        }

        var mse = sum / count;

        return 10 * Math.Log10(255.0 * 255.0 / Math.Max(mse, 1e-9));
    }

    /// <summary>The mean luma gradient (|dx| + |dy| of neighbours), border left out: how much edge contrast a picture has.</summary>
    public static double Gradient(byte[] image, int width, int height, int border)
    {
        double sum = 0;
        long count = 0;

        for (var y = border; y < height - border; y++)
        {
            for (var x = border; x < width - border; x++)
            {
                var index = ((y * width) + x) * 4;
                var centre = Luma(image, index);
                sum += Math.Abs(Luma(image, index + 4) - centre) + Math.Abs(Luma(image, index + (width * 4)) - centre);
                count++;
            }
        }

        return sum / count;
    }

    /// <summary>A rectangle of the picture.</summary>
    public static byte[] Crop(byte[] rgba, int width, int x, int y, int cropWidth, int cropHeight)
    {
        var result = new byte[cropWidth * cropHeight * 4];

        for (var row = 0; row < cropHeight; row++)
        {
            Array.Copy(rgba, (((y + row) * width) + x) * 4, result, row * cropWidth * 4, cropWidth * 4);
        }

        return result;
    }

    /// <summary>Pictures of one size side by side in a grid of <paramref name="columns"/>, a grey gap between.</summary>
    public static (byte[] Pixels, int Width, int Height) Montage(IReadOnlyList<byte[]> tiles, int tileWidth, int tileHeight, int columns)
    {
        const int Gap = 8;
        var rows = (tiles.Count + columns - 1) / columns;
        var width = (columns * tileWidth) + ((columns - 1) * Gap);
        var height = (rows * tileHeight) + ((rows - 1) * Gap);
        var result = new byte[width * height * 4];
        Array.Fill(result, (byte)96);

        for (var tile = 0; tile < tiles.Count; tile++)
        {
            var left = (tile % columns) * (tileWidth + Gap);
            var top = (tile / columns) * (tileHeight + Gap);

            for (var row = 0; row < tileHeight; row++)
            {
                Array.Copy(tiles[tile], row * tileWidth * 4, result, (((top + row) * width) + left) * 4, tileWidth * 4);
            }
        }

        return (result, width, height);
    }

    private static double Luma(byte[] image, int index)
    {
        return (0.2126 * image[index]) + (0.7152 * image[index + 1]) + (0.0722 * image[index + 2]);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
    }

    // One row of 8 x 8 blocks: forward DCT, quantise, inverse DCT, in place.
    private static void QuantiseRow(double[] plane, int width, int blockRow, int[] table, double scale)
    {
        var block = new double[64];
        var coefficients = new double[64];

        for (var bx = 0; bx < width / 8; bx++)
        {
            for (var index = 0; index < 64; index++)
            {
                block[index] = plane[(((blockRow * 8) + (index / 8)) * width) + (bx * 8) + (index % 8)];
            }

            Dct(block, coefficients, inverse: false);

            for (var index = 0; index < 64; index++)
            {
                var step = Math.Max(1, Math.Floor(((table[index] * scale) + 50) / 100));
                coefficients[index] = Math.Round(coefficients[index] / step) * step;
            }

            Dct(coefficients, block, inverse: true);

            for (var index = 0; index < 64; index++)
            {
                plane[(((blockRow * 8) + (index / 8)) * width) + (bx * 8) + (index % 8)] = block[index];
            }
        }
    }

    // The orthonormal 8-point DCT-II basis: Basis[k * 8 + n] = c(k) cos((2n + 1) k pi / 16).
    private static readonly double[] Basis = MakeBasis();

    // Separable: rows then columns; the inverse uses the transposed basis.
    private static void Dct(double[] source, double[] target, bool inverse)
    {
        var temp = new double[64];

        for (var row = 0; row < 8; row++)
        {
            for (var k = 0; k < 8; k++)
            {
                double sum = 0;

                for (var n = 0; n < 8; n++)
                {
                    var basis = inverse ? Basis[(n * 8) + k] : Basis[(k * 8) + n];
                    sum += source[(row * 8) + n] * basis;
                }

                temp[(row * 8) + k] = sum;
            }
        }

        for (var column = 0; column < 8; column++)
        {
            for (var k = 0; k < 8; k++)
            {
                double sum = 0;

                for (var n = 0; n < 8; n++)
                {
                    var basis = inverse ? Basis[(n * 8) + k] : Basis[(k * 8) + n];
                    sum += temp[(n * 8) + column] * basis;
                }

                target[(k * 8) + column] = sum;
            }
        }
    }

    private static double[] MakeBasis()
    {
        var basis = new double[64];

        for (var k = 0; k < 8; k++)
        {
            for (var n = 0; n < 8; n++)
            {
                var scale = k == 0 ? Math.Sqrt(1.0 / 8) : Math.Sqrt(2.0 / 8);
                basis[(k * 8) + n] = scale * Math.Cos(((2 * n) + 1) * k * Math.PI / 16);
            }
        }

        return basis;
    }

    // The chart, in truth pixels: four quadrants of 1920 x 1080.
    private static (double R, double G, double B) Chart(double u, double v)
    {
        var right = u >= 1920;
        var bottom = v >= 1080;
        var (qu, qv) = (u % 1920, v % 1080);

        if (!right && !bottom)
        {
            return Starburst(qu, qv);
        }

        if (right && !bottom)
        {
            return Glyphs(qu, qv);
        }

        if (!right)
        {
            return ZonePlate(qu, qv);
        }

        return Lines(qu, qv);
    }

    // Black and white wedges round a centre (edges at every angle, getting finer inwards).
    private static (double, double, double) Starburst(double u, double v)
    {
        var (dx, dy) = (u - 960, v - 540);
        var radius = Math.Sqrt((dx * dx) + (dy * dy));

        if (radius > 500 || radius < 20)
        {
            return (0.5, 0.5, 0.5);
        }

        var value = Math.Sin(36 * Math.Atan2(dy, dx)) >= 0 ? 0.92 : 0.06;

        return (value, value, value);
    }

    // Small glyphs (a 3 x 5 bitmap per 24 x 36 cell, strokes 3 truth pixels wide), dark on light, some coloured lines.
    private static (double, double, double) Glyphs(double u, double v)
    {
        var (cellX, cellY) = ((int)(u / 24), (int)(v / 36));
        var (inX, inY) = (u - (cellX * 24), v - (cellY * 36));
        var bits = (int)(HashUnit(cellX, cellY) * 32768);

        var column = (int)((inX - 4) / 5);
        var row = (int)((inY - 4) / 5.6);
        var ink = inX >= 4 && inX < 19 && inY >= 4 && inY < 32 && column is >= 0 and < 3 && row is >= 0 and < 5 && ((bits >> ((row * 3) + column)) & 1) != 0;
        var band = cellY % 6;

        if (!ink)
        {
            return (0.88, 0.87, 0.82);
        }

        return band switch
        {
            1 => (0.75, 0.1, 0.1),
            4 => (0.1, 0.25, 0.7),
            _ => (0.08, 0.08, 0.1),
        };
    }

    // Concentric rings whose frequency rises outwards, past what 1440p can hold at the edge.
    private static (double, double, double) ZonePlate(double u, double v)
    {
        var (dx, dy) = (u - 960, v - 540);
        var value = 0.5 + (0.45 * Math.Cos(((dx * dx) + (dy * dy)) * Math.PI / 2200));

        return (value, value, value);
    }

    // Thin lines at a few angles, coloured squares and checkerboards of 2 to 8 truth pixels.
    private static (double, double, double) Lines(double u, double v)
    {
        if (v < 520)
        {
            var angle = (int)(u / 480);
            var slope = new[] { 0.09, 0.58, 1.0, 5.7 }[angle];
            var distance = Math.Abs(((v - (slope * (u % 480))) % 24) + 24) % 24;
            var width = 1 + (angle % 2);

            if (distance < width)
            {
                return (0.05, 0.05, 0.05);
            }

            return (0.85, 0.85, 0.85);
        }

        if (u < 960)
        {
            var (bx, by) = ((int)(u / 80) % 3, (int)((v - 520) / 80) % 3);
            var inner = (u % 80) is > 16 and < 64 && ((v - 520) % 80) is > 16 and < 64;
            (double, double, double)[] colours = [(0.8, 0.1, 0.1), (0.1, 0.7, 0.2), (0.15, 0.2, 0.85)];

            var shift = inner ? 1 : 0;

            return colours[(bx + by + shift) % 3];
        }

        var size = new[] { 2, 3, 4, 6, 8, 12 }[Math.Min(5, (int)((u - 960) / 160))];
        var checker = (((int)(u / size)) + ((int)(v / size))) % 2 == 0;

        if (checker)
        {
            return (0.9, 0.9, 0.9);
        }

        return (0.1, 0.1, 0.1);
    }

    // A natural-looking texture: fractal noise coloured as earth and grass, fine blades, shaded pebbles.
    private static (double, double, double) Natural(double u, double v)
    {
        var broad = Fractal(u / 220, v / 220, 5);
        var fine = Fractal(u / 3.2, v / 9.0, 3);
        var grass = Math.Clamp((broad - 0.42) * 4, 0, 1);
        var (r, g, b) = Mix((0.42, 0.31, 0.2), (0.25, 0.48, 0.16), grass);
        var shade = 0.7 + (0.6 * (fine - 0.5)) + (0.25 * (Fractal(u / 40, v / 40, 3) - 0.5));
        (r, g, b) = (r * shade, g * shade, b * shade);

        var (cellX, cellY) = ((int)(u / 160), (int)(v / 160));
        var centreX = (cellX * 160) + 30 + (HashUnit(cellX, cellY) * 100);
        var centreY = (cellY * 160) + 30 + (HashUnit(cellY, cellX + 7) * 100);
        var radius = 14 + (HashUnit(cellX + 3, cellY + 5) * 22);
        var (dx, dy) = ((u - centreX) / radius, (v - centreY) / (radius * 0.75));
        var inside = (dx * dx) + (dy * dy);

        if (inside < 1)
        {
            var light = 0.45 + (0.5 * Math.Clamp(1 - inside - (0.5 * (dx + dy)), 0, 1));

            return (0.62 * light, 0.6 * light, 0.56 * light);
        }

        return (r, g, b);
    }

    private static (double, double, double) Mix((double, double, double) a, (double, double, double) b, double t)
    {
        return (a.Item1 + ((b.Item1 - a.Item1) * t), a.Item2 + ((b.Item2 - a.Item2) * t), a.Item3 + ((b.Item3 - a.Item3) * t));
    }

    private static double Fractal(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 0.5, total = 0;

        for (var octave = 0; octave < octaves; octave++)
        {
            sum += amplitude * ValueNoise(x, y, octave);
            total += amplitude;
            (x, y, amplitude) = (x * 2.03, y * 2.03, amplitude * 0.5);
        }

        return sum / total;
    }

    private static double ValueNoise(double x, double y, int seed)
    {
        var (ix, iy) = ((int)Math.Floor(x), (int)Math.Floor(y));
        var (fx, fy) = (x - ix, y - iy);
        (fx, fy) = (fx * fx * (3 - (2 * fx)), fy * fy * (3 - (2 * fy)));
        var top = Lerp(HashUnit(ix + (seed * 131), iy), HashUnit(ix + 1 + (seed * 131), iy), fx);
        var bottom = Lerp(HashUnit(ix + (seed * 131), iy + 1), HashUnit(ix + 1 + (seed * 131), iy + 1), fx);

        return Lerp(top, bottom, fy);
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + ((b - a) * t);
    }

    private static double HashUnit(int x, int y)
    {
        var hash = (uint)((x * 73856093) ^ (y * 19349663));
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;

        return (hash & 0xFFFFFF) / (double)0x1000000;
    }
}
