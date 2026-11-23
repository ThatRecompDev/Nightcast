// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.ComponentModel;
using System.Diagnostics;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The system's <c>xz</c> (part of every SteamOS and Arch install: pacman and the kernel's modules need it) decompresses
/// the archive to standard output, which is written to the tar file.
/// </summary>
public sealed class XzDecompressor : IArchiveDecompressor
{
    private const string Program = "xz";

    public bool TryDecompress(string archive, string tar, out string? problem)
    {
        var start = new ProcessStartInfo(Program) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--decompress");
        start.ArgumentList.Add("--stdout");
        start.ArgumentList.Add(archive);

        try
        {
            using var process = Process.Start(start);

            if (process is null)
            {
                problem = "xz did not start";

                return false;
            }

            return Finish(process, tar, out problem);
        }
        catch (Win32Exception missing)
        {
            problem = $"xz could not run ({missing.Message})";

            return false;
        }
    }

    /// <summary>Its output into <paramref name="tar"/>, then its exit: 0 is done.</summary>
    private static bool Finish(Process process, string tar, out string? problem)
    {
        var errors = process.StandardError.ReadToEndAsync();

        using (var file = File.Create(tar))
        {
            process.StandardOutput.BaseStream.CopyTo(file);
        }

        process.WaitForExit();
        problem = null;

        if (process.ExitCode != 0)
        {
            problem = $"xz failed: {errors.Result.Trim()}";

            return false;
        }

        return true;
    }
}
