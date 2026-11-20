// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Nvidia.Interop;

namespace Phantom.Relay.Nvidia;

/// <summary>
/// Finds the folder of the owner's own NvOFFRUC.dll (with the CUDA runtime it ships with): the configured folder (or
/// the library's own path) when the library is there, otherwise the sample's binaries of each Optical_Flow_SDK_*
/// folder directly under the search roots the caller gives (the drives' roots, the user's folders), the newest SDK
/// version first. No path is stored in the repository; the caller names the roots at run time.
/// </summary>
public static class NvidiaFrucLocator
{
    /// <summary>The SDK's folder as NVIDIA's archive unpacks it: Optical_Flow_SDK_5.0.7 and the like.</summary>
    public const string SdkFolderPattern = "Optical_Flow_SDK_*";

    /// <summary>Where the 64-bit library sits inside the SDK's folder.</summary>
    public static readonly string BinaryFolder = Path.Combine("NvOFFRUC", "NvOFFRUCSample", "bin", "win64");

    /// <summary>The folder holding NvOFFRUC.dll: the configured one, else the newest SDK's under the roots, else null.</summary>
    public static string? Find(string? configured, IEnumerable<string> roots)
    {
        var chosen = Configured(configured);

        if (chosen is not null)
        {
            return chosen;
        }

        foreach (var root in roots)
        {
            var found = Search(root);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    // A folder holding the library, or the library's own path.
    private static string? Configured(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        if (HoldsLibrary(configured))
        {
            return configured;
        }

        var folder = Path.GetDirectoryName(configured);

        if (File.Exists(configured) && folder is not null && HoldsLibrary(folder))
        {
            return folder;
        }

        return null;
    }

    // The SDK folders under one root, newest version first (their names sort by version).
    private static string? Search(string root)
    {
        string[] sdks;

        try
        {
            sdks = Directory.Exists(root) ? Directory.GetDirectories(root, SdkFolderPattern) : [];
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        Array.Sort(sdks, StringComparer.OrdinalIgnoreCase);

        for (var index = sdks.Length - 1; index >= 0; index--)
        {
            var candidate = Path.Combine(sdks[index], BinaryFolder);

            if (HoldsLibrary(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HoldsLibrary(string folder)
    {
        return File.Exists(Path.Combine(folder, FrucValues.LibraryFile));
    }
}
