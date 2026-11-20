// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// ffmpeg's arguments for a comparison, one list entry per argument (for <c>ProcessStartInfo.ArgumentList</c>: no shell,
/// no quoting): the source on the left and the shown pictures on the right (<c>hstack</c>), the shown file started its
/// offset later (<c>-itsoffset</c>) so the two show the same moment, the shown side scaled to the source's size, both
/// brought to one rate (<c>fps</c>) and labelled (<c>drawtext</c>), at half speed by doubling the times (<c>setpts</c>),
/// encoded on the GPU at high quality: <c>h264_nvenc</c>, or <c>hevc_nvenc</c> when the pair is wider than NVENC's H.264
/// takes (two 1440p pictures are 5120 wide). The progress goes to standard output (<c>-progress pipe:1</c>).
/// </summary>
internal static class CompareArguments
{
    /// <summary>The widest picture NVENC's H.264 encoder takes.</summary>
    public const int MaxH264Width = 4096;

    public const string H264Encoder = "h264_nvenc";
    public const string HevcEncoder = "hevc_nvenc";

    // NVENC's slowest, best preset and a constant quality that keeps the generated frames' artifacts visible.
    private const string Preset = "p7";
    private const string ConstantQuality = "18";

    // The labels' size, margin and box border as fractions of the picture's height.
    private const int FontDivisor = 36;
    private const int MarginDivisor = 48;
    private const int BorderDivisor = 120;
    private const int Sides = 2;

    private const string SecondsFormat = "0.000000";
    private const string LabelCharacters = " .()+-";

    /// <summary>The arguments for <paramref name="request"/>.</summary>
    public static IReadOnlyList<string> Build(CompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var offset = Math.Max(0, request.ShownOffset.TotalSeconds).ToString(SecondsFormat, CultureInfo.InvariantCulture);
        var encoder = EncoderFor(request.Width);
        List<string> arguments =
        [
            "-hide_banner", "-nostdin", "-y", "-loglevel", "error", "-nostats", "-progress", "pipe:1",
            "-i", request.Source,
            "-itsoffset", offset, "-i", request.Shown,
            "-filter_complex", Graph(request),
            "-map", "[v]", "-an",
            "-c:v", encoder, "-preset", Preset, "-tune", "hq", "-rc", "vbr", "-cq", ConstantQuality, "-b:v", "0", "-pix_fmt", "yuv420p",
        ];

        if (encoder == HevcEncoder)
        {
            // The tag QuickTime and Windows' own player expect of HEVC in MP4.
            arguments.AddRange(["-tag:v", "hvc1"]);
        }

        arguments.AddRange(["-fps_mode", "passthrough", "-movflags", "+faststart", request.Output]);

        return arguments;
    }

    /// <summary>The encoder for a pair of pictures <paramref name="width"/> wide each.</summary>
    public static string EncoderFor(int width)
    {
        if (width * Sides > MaxH264Width)
        {
            return HevcEncoder;
        }

        return H264Encoder;
    }

    /// <summary>The filter graph: each side at the rate and labelled, the shown side at the source's size, side by side.</summary>
    public static string Graph(CompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rate = request.FramesPerSecond.ToString(CultureInfo.InvariantCulture);
        var size = string.Create(CultureInfo.InvariantCulture, $"{request.Width}:{request.Height}");
        var graph = new StringBuilder();
        graph.Append(CultureInfo.InvariantCulture, $"[0:v]fps={rate},{Label(request, request.SourceLabel)}[a];");
        graph.Append(CultureInfo.InvariantCulture, $"[1:v]scale={size},fps={rate},{Label(request, request.ShownLabel)}[b];");
        graph.Append("[a][b]hstack=inputs=2:shortest=1");

        if (request.HalfSpeed)
        {
            graph.Append(",setpts=2*PTS");
        }

        graph.Append("[v]");

        return graph.ToString();
    }

    /// <summary>
    /// A path as a filter option's value: forward slashes, the drive's colon escaped, in single quotes; null when the path
    /// holds a quote (it cannot be written then, and ffmpeg's own font is used).
    /// </summary>
    public static string? FilterPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Contains('\'', StringComparison.Ordinal))
        {
            return null;
        }

        return $"'{path.Replace('\\', '/').Replace(":", "\\:", StringComparison.Ordinal)}'";
    }

    /// <summary>
    /// A label as drawtext writes it: letters, digits and a few plain marks only (no quote, colon, comma or percent, which
    /// the filter graph or drawtext would read as their own).
    /// </summary>
    public static string LabelText(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var text = new StringBuilder(label.Length);

        foreach (var character in label)
        {
            if (char.IsAsciiLetterOrDigit(character) || LabelCharacters.Contains(character, StringComparison.Ordinal))
            {
                text.Append(character);
            }
        }

        return text.ToString();
    }

    private static string Label(CompareRequest request, string label)
    {
        var height = Math.Max(1, request.Height);
        var text = new StringBuilder("drawtext=");
        var font = request.FontFile is null ? null : FilterPath(request.FontFile);

        if (font is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"fontfile={font}:");
        }

        text.Append(CultureInfo.InvariantCulture, $"text='{LabelText(label)}':x={height / MarginDivisor}:y={height / MarginDivisor}");
        text.Append(CultureInfo.InvariantCulture, $":fontsize={height / FontDivisor}:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw={height / BorderDivisor}");

        return text.ToString();
    }
}
