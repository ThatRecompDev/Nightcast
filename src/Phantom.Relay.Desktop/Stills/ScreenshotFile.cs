// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Stream.Setup;

namespace Phantom.Relay.Desktop.Stills;

/// <summary>
/// Where a screenshot goes and what it is called: <c>Nightcast\2026-11-23 21-14-03.png</c> under the user's
/// screenshots folder (the local time it was taken, as a recording's folder is named; " (2)" and on when one of that
/// second exists), never under the repository.
/// </summary>
internal static class ScreenshotFile
{
    /// <summary>A screenshot's extension.</summary>
    public const string Extension = ".png";

    /// <summary>What a screenshot is called while it is written: its own name and this; renamed once whole.</summary>
    public const string PartialSuffix = ".partial";

    private const int FirstRepeat = 2;

    /// <summary>
    /// The file for a screenshot taken at <paramref name="localTime"/> under <paramref name="root"/>: the stamp, or the
    /// stamp and " (2)", " (3)" and so on when <paramref name="exists"/> says that one is taken.
    /// </summary>
    public static string For(string root, DateTimeOffset localTime, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(exists);

        var folder = Path.Combine(root, RelayApp.Name);
        var stamp = RecordingFolder.Stamp(localTime);
        var file = Path.Combine(folder, stamp + Extension);

        for (var repeat = FirstRepeat; exists(file); repeat++)
        {
            file = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stamp} ({repeat}){Extension}"));
        }

        return file;
    }

    /// <summary>The file as the window shows it: the ellipsis, then <c>Nightcast\2026-11-23 21-14-03.png</c>.</summary>
    public static string Short(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        return Path.Combine(RecordingFolder.Ellipsis, RelayApp.Name, Path.GetFileName(file));
    }
}
