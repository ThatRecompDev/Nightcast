// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// Where the recorder writes and what it runs, found once at start: the user's Videos folder, the user's own ffmpeg
/// (null when there is none) and a monospaced font for the comparison's labels (null: ffmpeg's own).
/// </summary>
internal sealed record RecordingPlaces(string Videos, string? Ffmpeg, string? FontFile)
{
    private const string VideosFolder = "Videos";
    private const string PathVariable = "PATH";

    // Consolas, else Arial: both in every Windows install.
    private static readonly string[] Fonts = ["consola.ttf", "arial.ttf"];

    /// <summary>This user's places on this machine.</summary>
    public static RecordingPlaces Find()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        if (string.IsNullOrEmpty(videos))
        {
            videos = Path.Combine(profile, VideosFolder);
        }

        var ffmpeg = FfmpegLocator.Find(Environment.GetEnvironmentVariable(PathVariable), profile, File.Exists);

        return new RecordingPlaces(videos, ffmpeg, FindFont());
    }

    private static string? FindFont()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        foreach (var font in Fonts)
        {
            var path = Path.Combine(folder, font);

            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
