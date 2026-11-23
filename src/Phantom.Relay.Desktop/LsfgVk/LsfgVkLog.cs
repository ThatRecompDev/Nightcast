// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// lsfg-vk's own log (the config's <c>log_file</c>), as 2.0.0 writes it when the layer starts:
/// <code>
/// (lsfg-vk) [INFO]: Loaded lsfg-vk layer version 2.0.0
/// (lsfg-vk) [INFO]: Using profile with name 'Nightcast' (identified via environment)
/// </code>
/// Read from where it ended before an instance was made, so earlier runs' lines do not count.
/// </summary>
public static class LsfgVkLog
{
    private const string LoadedPrefix = "Loaded lsfg-vk layer version ";
    private const string ProfileLine = "Using profile with name 'Nightcast'";
    private const string ErrorPrefix = "[ERROR]:";

    /// <summary>The log's length now; 0 when there is none yet.</summary>
    public static long Length(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>What the log gained after <paramref name="from"/> bytes; empty when it cannot be read.</summary>
    public static string ReadFrom(string path, long from)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (from > file.Length)
            {
                from = 0;
            }

            file.Seek(from, SeekOrigin.Begin);
            using var reader = new StreamReader(file);

            return reader.ReadToEnd();
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>The last error line lsfg-vk wrote, without its prefix, and the detail line after it; null when none.</summary>
    public static string? LastError(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string? last = null;

        foreach (var line in text.Split('\n'))
        {
            int at = line.IndexOf(ErrorPrefix, StringComparison.Ordinal);

            if (at >= 0)
            {
                last = Join(last, line[(at + ErrorPrefix.Length)..].Trim());
            }
        }

        return last;
    }

    /// <summary>Error lines in a row read as one ("…swapchain: - createDeviceUnique: -7").</summary>
    private static string Join(string? earlier, string line)
    {
        if (earlier is null || !earlier.EndsWith(':'))
        {
            return line;
        }

        return $"{earlier} {line}";
    }

    /// <summary>The layer's version when the text says it loaded and took Nightcast's profile; else null.</summary>
    public static string? LoadedVersion(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.Contains(ProfileLine, StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var line in text.Split('\n'))
        {
            int at = line.IndexOf(LoadedPrefix, StringComparison.Ordinal);

            if (at >= 0)
            {
                return line[(at + LoadedPrefix.Length)..].Trim();
            }
        }

        return null;
    }
}
