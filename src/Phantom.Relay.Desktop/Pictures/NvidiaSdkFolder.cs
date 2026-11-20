// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Nvidia;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// Where the owner's NVIDIA Optical Flow SDK is, for FRUC: the folder PHANTOM_NVOFFRUC_DIR names (the library's folder
/// or its path), else the newest Optical_Flow_SDK_* folder at the root of a fixed drive, in the user's folder or in
/// their Downloads (<see cref="NvidiaFrucLocator"/>). Nothing of it is stored; it is looked for when FRUC is first asked for.
/// </summary>
internal static class NvidiaSdkFolder
{
    /// <summary>The environment variable that names the folder holding NvOFFRUC.dll.</summary>
    public const string Variable = "PHANTOM_NVOFFRUC_DIR";

    private const string Downloads = "Downloads";

    /// <summary>The folder holding NvOFFRUC.dll and its CUDA runtime, or null.</summary>
    public static string? Find()
    {
        return NvidiaFrucLocator.Find(Environment.GetEnvironmentVariable(Variable), Roots());
    }

    private static IEnumerable<string> Roots()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
            {
                yield return drive.RootDirectory.FullName;
            }
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrEmpty(profile))
        {
            yield return profile;

            yield return Path.Combine(profile, Downloads);
        }
    }
}
