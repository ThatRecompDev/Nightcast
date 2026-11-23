// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Places;

/// <summary>
/// The user's own folders Nightcast writes into, one rule for every head and lane: recordings under the Videos folder
/// (Linux: <c>$XDG_VIDEOS_DIR</c>, else the user-dirs Videos, else <c>~/Videos</c>), screenshots under the Pictures
/// folder on Linux (<c>$XDG_PICTURES_DIR</c>, likewise) and beside the recordings in Videos on Windows, each in a
/// <c>Nightcast</c> folder. Never under the repository.
/// </summary>
public static class UserFolders
{
    /// <summary>The XDG user-dirs variable of the Videos folder.</summary>
    public const string VideosVariable = "XDG_VIDEOS_DIR";

    /// <summary>The XDG user-dirs variable of the Pictures folder.</summary>
    public const string PicturesVariable = "XDG_PICTURES_DIR";

    private const string VideosName = "Videos";
    private const string PicturesName = "Pictures";

    /// <summary>This user's Videos folder (recordings go in its <c>Nightcast</c> folder).</summary>
    public static string Videos()
    {
        return Pick(Variable(VideosVariable), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Home(), VideosName);
    }

    /// <summary>The folder whose <c>Nightcast</c> folder takes the screenshots: Pictures on Linux, Videos on Windows.</summary>
    public static string Screenshots()
    {
        if (OperatingSystem.IsWindows())
        {
            return Videos();
        }

        return Pick(Variable(PicturesVariable), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Home(), PicturesName);
    }

    /// <summary>
    /// A user folder: the XDG variable's (Linux only, <paramref name="variable"/>), else the system's
    /// (<paramref name="special"/>), else <paramref name="fallback"/> in <paramref name="home"/>.
    /// </summary>
    public static string Pick(string? variable, string? special, string home, string fallback)
    {
        if (!string.IsNullOrEmpty(variable))
        {
            return variable;
        }

        if (!string.IsNullOrEmpty(special))
        {
            return special;
        }

        return Path.Combine(home, fallback);
    }

    // The XDG variables are Linux's; elsewhere they are not read.
    private static string? Variable(string name)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(name);
    }

    private static string Home()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
