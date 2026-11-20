// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.App.Recording;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The recording's labels and rates: "console 30 fps" over the source, "Relay LSFG x2" over what was shown, the
/// multiplier frame generation really ran at (1 when it was off, whatever was chosen) and the rate the shown pictures
/// come at (the console's 60 units a second, or a 30 fps game's new pictures times the multiplier).
/// </summary>
internal static class CompareLabels
{
    /// <summary>The console's units a second.</summary>
    public const int ConsoleRate = 60;

    /// <summary>The game rate frame generation is made for (a 30 fps game's new pictures).</summary>
    public const int GameRate = 30;

    private const string ConsoleName = "console";
    private const string WithoutGeneration = "Relay without frame generation";
    private const char Space = ' ';

    /// <summary>The multiplier the shown pictures came at: the chosen one while frame generation ran, else 1.</summary>
    public static int Multiplier(RecordingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Multiplier < FrameGeneration.Doubled || settings.FrameGeneration.StartsWith(GpuPictureReport.GenerationOff, StringComparison.Ordinal))
        {
            return 1;
        }

        return settings.Multiplier;
    }

    /// <summary>The rate the shown pictures come at for <paramref name="multiplier"/>: 60, 90 or 120.</summary>
    public static int ShownRate(int multiplier)
    {
        return Math.Max(ConsoleRate, GameRate * multiplier);
    }

    /// <summary>"console 30 fps", from the new pictures a second the stream counted (just "console" before any).</summary>
    public static string Source(double newPicturesPerSecond)
    {
        if (newPicturesPerSecond < 1)
        {
            return ConsoleName;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{ConsoleName} {Math.Round(newPicturesPerSecond):0} fps");
    }

    /// <summary>"Relay LSFG x2": the generator's name (the first word of its description) and the multiplier.</summary>
    public static string Shown(RecordingSettings settings)
    {
        var multiplier = Multiplier(settings);

        if (multiplier < FrameGeneration.Doubled)
        {
            return WithoutGeneration;
        }

        var description = settings.FrameGeneration.Trim();
        var end = description.IndexOf(Space, StringComparison.Ordinal);
        var name = end < 0 ? description : description[..end];

        return string.Create(CultureInfo.InvariantCulture, $"Relay {name} x{multiplier}");
    }
}
