// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text;
using Phantom.Relay.Media.Windows.Recording;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// recording.txt: a few plain lines on what was recorded and how (the frame generation, its multiplier, the upscaler,
/// each file's frames, size, length and bitrate, what was dropped and why, and the comparison). It names no account,
/// gamertag, console or path: only the settings and the counts.
/// </summary>
internal static class RecordingReport
{
    private const string NotMade = "not made";

    /// <summary>The report of <paramref name="summary"/>, with <paramref name="compare"/> saying what became of the comparison.</summary>
    public static string Text(RecordingSummary summary, string compare)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var text = new StringBuilder();
        Header(text, summary);

        Source(text, summary.Source);
        text.AppendLine();
        Shown(text, summary.Shown);
        text.AppendLine();

        text.AppendLine(CultureInfo.InvariantCulture, $"{RecordingFolder.CompareFile}, {RecordingFolder.HalfSpeedFile}: {compare}");

        return text.ToString();
    }

    /// <summary>"2541 frames (2 key), 2560x1440, 42.350 s, H.264, 31.2 Mbit/s", or why the file was not made.</summary>
    public static string Describe(Mp4Facts? facts, string? problem)
    {
        if (facts is null)
        {
            return $"{NotMade}: {problem ?? "nothing was recorded"}";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{facts.Frames} frames ({facts.KeyFrames} key), {facts.Width}x{facts.Height}, {facts.Duration.TotalSeconds:F3} s, {facts.Codec}, {facts.MegabitsPerSecond:F1} Mbit/s");
    }

    private static void Header(StringBuilder text, RecordingSummary summary)
    {
        text.AppendLine("Phantom.Relay recording");
        text.AppendLine(CultureInfo.InvariantCulture, $"started {summary.StartedAt:yyyy-MM-dd HH:mm:ss} (local time)");
        text.AppendLine(CultureInfo.InvariantCulture, $"frame generation: {summary.Settings.FrameGeneration} (shown at x{summary.Multiplier}, {summary.ShownRate} fps)");
        text.AppendLine(CultureInfo.InvariantCulture, $"upscaler: {summary.Settings.Upscaler}");
        text.AppendLine(CultureInfo.InvariantCulture, $"labels: \"{summary.SourceLabel}\" and \"{summary.ShownLabel}\"");
        text.AppendLine();
    }

    private static void Source(StringBuilder text, SourceOutcome source)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"{RecordingFolder.SourceFile}: the console's H.264 as received (repeats included), remuxed without re-encoding");
        text.AppendLine(CultureInfo.InvariantCulture, $"  {Describe(source.Facts, source.Problem)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  units written {source.Written}; skipped before the first key frame {source.Skipped}; dropped (writer behind) {source.Dropped}; refused by the file {source.Failed}");
    }

    private static void Shown(StringBuilder text, ShownOutcome shown)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"{RecordingFolder.ShownFile}: every picture Relay showed (decoded and generated) at the time it was shown, encoded on the GPU by {shown.Encoder}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  {Describe(shown.Facts, shown.Problem)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  starts {shown.Offset.TotalSeconds:F3} s after {RecordingFolder.SourceFile}; pictures written {shown.Written}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  not recorded: before the encoder was ready {shown.Warming}, encoder behind {shown.Busy}, queue full {shown.Dropped}, another size {shown.Resized}, refused {shown.Failed}");
    }
}
