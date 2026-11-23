// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// Nightcast's own lsfg-vk configuration, in lsfg-vk 2.0's TOML (its <c>lsfg-vk-config/src/config.cpp</c>, which
/// rejects unknown keys): <c>version = 2</c>; <c>[global]</c> with the user's <c>dll</c>, half precision allowed
/// (lsfg-vk's default: large gains on AMD) and the layer's <c>log_file</c>; one <c>[[profile]]</c> named
/// <see cref="LsfgVkRelease.ProfileName"/>, which <c>LSFGVK_PROFILE</c> picks, with vsync pacing (the only mode) and
/// the player's multiplier, flow scale and performance mode. Off is multiplier 1, which lsfg-vk takes as "pass the
/// presents through". The layer watches the file (inotify on its folder) and takes the multiplier, flow scale and
/// performance mode live, so the file is replaced whole by a rename (<see cref="Save"/>), never written in place.
/// </summary>
public static class LsfgVkConfig
{
    /// <summary>lsfg-vk's "no frames generated": the presents pass through.</summary>
    public const int Disabled = 1;

    private const string NewSuffix = ".new";
    private const char Backslash = '\\';
    private const char Quote = '"';
    private const char FirstPrintable = ' ';
    private const char Delete = '\u007f';

    /// <summary>The file's text for <paramref name="settings"/>, the Lossless Scaling file <paramref name="dll"/> and the log at <paramref name="log"/>.</summary>
    public static string Text(FrameGeneration settings, string dll, string log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var text = new StringBuilder();
        text.Append("version = 2\n\n[global]\n");
        text.Append(CultureInfo.InvariantCulture, $"dll = {Quoted(dll)}\n");
        text.Append("allow_fp16 = true\n");
        text.Append(CultureInfo.InvariantCulture, $"log_file = {Quoted(log)}\n\n");

        text.Append("[[profile]]\n");
        text.Append(CultureInfo.InvariantCulture, $"name = {Quoted(LsfgVkRelease.ProfileName)}\n");
        text.Append("pacing_mode = \"vsync\"\n");
        text.Append(CultureInfo.InvariantCulture, $"multiplier = {Multiplier(settings)}\n");
        text.Append(CultureInfo.InvariantCulture, $"flow_scale = {settings.ClampedFlowScale:0.0##}\n");
        text.Append(CultureInfo.InvariantCulture, $"performance_mode = {Flag(settings.Mode == FrameGenerationMode.Performance)}\n");

        return text.ToString();
    }

    /// <summary>The frames per real one lsfg-vk makes for <paramref name="settings"/>: its multiplier when on, else <see cref="Disabled"/>.</summary>
    public static int Multiplier(FrameGeneration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsOn)
        {
            return Disabled;
        }

        return settings.ClampedMultiplier;
    }

    /// <summary>Replaces the file at <paramref name="path"/> with <paramref name="text"/> in one rename, as the layer's watcher expects.</summary>
    public static void Save(string path, string text)
    {
        var written = path + NewSuffix;
        File.WriteAllText(written, text, new UTF8Encoding(false));
        File.Move(written, path, overwrite: true);
    }

    /// <summary>A TOML basic string: quoted, its backslashes, quotes and control characters escaped.</summary>
    public static string Quoted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var quoted = new StringBuilder();
        quoted.Append(Quote);

        foreach (var character in value)
        {
            if (character is Backslash or Quote)
            {
                quoted.Append(Backslash).Append(character);
            }
            else if (character < FirstPrintable || character == Delete)
            {
                quoted.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
            }
            else
            {
                quoted.Append(character);
            }
        }

        return quoted.Append(Quote).ToString();
    }

    private static string Flag(bool on)
    {
        if (on)
        {
            return "true";
        }

        return "false";
    }
}
