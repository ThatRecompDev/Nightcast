// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Updates;

/// <summary>Which files of a release are one system's build: the build and its checksum file.</summary>
public sealed record UpdateAssets(string Build, string Checksum)
{
    public static UpdateAssets Windows { get; } = new(UpdateSource.ExeName, UpdateSource.ChecksumName);

    public static UpdateAssets Linux { get; } = new(UpdateSource.LinuxName, UpdateSource.LinuxChecksumName);

    /// <summary>The build of the system named; null for one Nightcast publishes no build for.</summary>
    public static UpdateAssets? For(bool windows, bool linux)
    {
        if (windows)
        {
            return Windows;
        }

        if (linux)
        {
            return Linux;
        }

        return null;
    }

    /// <summary>The build of the system this runs on; null where there is none (macOS).</summary>
    public static UpdateAssets? Running()
    {
        return For(OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
    }
}
