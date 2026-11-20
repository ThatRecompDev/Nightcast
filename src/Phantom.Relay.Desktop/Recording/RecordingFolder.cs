// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// Where a recording goes and what its files are called: a folder per recording under the user's Videos folder,
/// <c>Videos\Phantom.Relay\2026-11-20 21-14-03</c> (the local time it started; " (2)" and on when one of that second
/// exists), never under the repository.
/// </summary>
internal static class RecordingFolder
{
    /// <summary>The folder every recording's own folder is in, under the user's Videos folder.</summary>
    public const string AppFolder = "Phantom.Relay";

    /// <summary>The console's H.264 as it came, remuxed without re-encoding.</summary>
    public const string SourceFile = "source.mp4";

    /// <summary>Every picture Relay showed, encoded on the GPU.</summary>
    public const string ShownFile = "shown.mp4";

    /// <summary>The two side by side, synced and labelled (ffmpeg).</summary>
    public const string CompareFile = "compare.mp4";

    /// <summary>The same at half speed.</summary>
    public const string HalfSpeedFile = "compare-half-speed.mp4";

    /// <summary>The recording's settings and counts.</summary>
    public const string ReportFile = "recording.txt";

    /// <summary>The stamp a folder is named by: the local time, to the second, with no character a path refuses.</summary>
    public const string StampFormat = "yyyy-MM-dd HH-mm-ss";

    /// <summary>What stands for the folders above the app's in the window: a horizontal ellipsis.</summary>
    public const string Ellipsis = "…";

    private const int FirstRepeat = 2;

    /// <summary>A recording's folder name for a start at <paramref name="localTime"/>.</summary>
    public static string Stamp(DateTimeOffset localTime)
    {
        return localTime.ToString(StampFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The folder for a recording started at <paramref name="localTime"/> under <paramref name="videos"/>: the stamp,
    /// or the stamp and " (2)", " (3)" and so on when <paramref name="exists"/> says that one is taken.
    /// </summary>
    public static string For(string videos, DateTimeOffset localTime, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrEmpty(videos);
        ArgumentNullException.ThrowIfNull(exists);

        var root = Path.Combine(videos, AppFolder);
        var stamp = Stamp(localTime);
        var folder = Path.Combine(root, stamp);

        for (var repeat = FirstRepeat; exists(folder); repeat++)
        {
            folder = Path.Combine(root, string.Create(CultureInfo.InvariantCulture, $"{stamp} ({repeat})"));
        }

        return folder;
    }

    /// <summary>The folder as the window shows it: the ellipsis, then <c>Phantom.Relay\2026-11-20 21-14-03</c>.</summary>
    public static string Short(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        return Path.Combine(Ellipsis, AppFolder, Path.GetFileName(folder));
    }
}
