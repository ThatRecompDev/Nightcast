// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// Runs the user's ffmpeg for a comparison (<see cref="CompareArguments"/>, each argument on its own: no shell, no
/// quoting), reading its progress from standard output as it goes and the end of its errors when it fails. On the
/// recorder's finishing thread, never the window's.
/// </summary>
internal sealed class CompareRunner
{
    private const int ErrorLinesKept = 3;

    private readonly string _ffmpeg;

    public CompareRunner(string ffmpeg)
    {
        _ffmpeg = ffmpeg;
    }

    /// <summary>
    /// Makes <paramref name="request"/>'s file, telling <paramref name="progress"/> how far it is (whole percent of
    /// <paramref name="length"/>, the output's length); why it failed, or null.
    /// </summary>
    public string? Run(CompareRequest request, TimeSpan length, Action<int> progress)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        var start = new ProcessStartInfo(_ffmpeg)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in CompareArguments.Build(request))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);

        if (process is null)
        {
            return "ffmpeg did not start";
        }

        var errors = process.StandardError.ReadToEndAsync();
        Follow(process.StandardOutput, length, progress);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return $"ffmpeg ended with {process.ExitCode}: {LastLines(errors.GetAwaiter().GetResult())}";
        }

        return null;
    }

    private static void Follow(StreamReader output, TimeSpan length, Action<int> progress)
    {
        while (true)
        {
            var line = output.ReadLine();

            if (line is null)
            {
                return;
            }

            if (FfmpegProgress.TryRead(line, out var time))
            {
                progress(FfmpegProgress.Percent(time, length));
            }
        }
    }

    private static string LastLines(string errors)
    {
        var lines = errors.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(" | ", lines.TakeLast(ErrorLinesKept));
    }
}
