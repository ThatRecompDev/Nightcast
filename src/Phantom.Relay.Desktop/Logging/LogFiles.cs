// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Stream.Setup;

namespace Phantom.Relay.Desktop.Logging;

/// <summary>
/// A log file per run, beside the console's error stream, so a stream that failed on a handheld can be read afterwards:
/// <c>%LOCALAPPDATA%\Nightcast\logs\2026-11-23 12-05-41.log</c>; only the newest <see cref="Kept"/> stay. It holds what
/// the console window shows (states, stats, failures), never a token. No file when the folder cannot be written.
/// </summary>
public static class LogFiles
{
    /// <summary>NOT DATA: how many runs' logs are kept.</summary>
    public const int Kept = 10;

    private const string Folder = "logs";
    private const string Extension = ".log";
    private const string Pattern = "*" + Extension;
    private const string StampFormat = "yyyy-MM-dd HH-mm-ss";

    /// <summary>The new run's log, written through at once; null when it could not be made.</summary>
    public static StreamWriter? Open(DateTimeOffset now)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), RelayApp.Name, Folder);
            Directory.CreateDirectory(folder);
            Prune(folder);

            var path = Path.Combine(folder, now.ToString(StampFormat, CultureInfo.InvariantCulture) + Extension);

            return new StreamWriter(path, append: true) { AutoFlush = true };
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Removes the oldest logs so that, with the new one, <see cref="Kept"/> remain.</summary>
    private static void Prune(string folder)
    {
        var old = new DirectoryInfo(folder).GetFiles(Pattern).OrderByDescending(file => file.Name, StringComparer.Ordinal).Skip(Kept - 1);

        foreach (var file in old)
        {
            file.Delete();
        }
    }
}
