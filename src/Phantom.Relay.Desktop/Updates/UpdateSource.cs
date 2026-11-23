// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Updates;

/// <summary>
/// Where Nightcast's public builds are (docs/how-we-work.md 1a): the latest release of a public GitHub repository that
/// holds only a README and the releases, read anonymously through GitHub's REST API. A release carries each system's
/// build (the Windows exe, the Linux file) and a file with its SHA-256; a build downloads only its own system's.
/// </summary>
public static class UpdateSource
{
    public const string Repository = "ThatRecompDev/Nightcast";

    /// <summary>The exe's name in a release, and the name a published build runs under.</summary>
    public const string ExeName = "Nightcast.exe";

    /// <summary>The release's checksum file: the exe's SHA-256 in hex first, as sha256sum writes it.</summary>
    public const string ChecksumName = "Nightcast.exe.sha256";

    /// <summary>The Linux build's name in a release (64-bit x86, SteamOS), and its checksum file's.</summary>
    public const string LinuxName = "Nightcast-linux-x64";

    public const string LinuxChecksumName = "Nightcast-linux-x64.sha256";

    /// <summary>GitHub's API asks every client for a User-Agent; the app's name, nothing of the user's.</summary>
    public const string UserAgent = "Nightcast-Updater";

    /// <summary>The release's tag is its version after this prefix: v2026.11.23.1135.</summary>
    public const string TagPrefix = "v";

    public static readonly Uri LatestRelease = new($"https://api.github.com/repos/{Repository}/releases/latest");

    /// <summary>Every download comes from below this address (GitHub then redirects to its file storage).</summary>
    public static readonly string DownloadPrefix = $"https://github.com/{Repository}/releases/download/";

    /// <summary>NOT DATA: how long the check may take before the app starts without it (offline, GitHub slow).</summary>
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    /// <summary>NOT DATA: how long the exe's download may take.</summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
}
