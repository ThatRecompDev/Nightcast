// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Globalization;

namespace Phantom.Relay.Desktop.Updates;

/// <summary>
/// The second half of an update. The old build starts the downloaded <c>.new</c> exe with
/// <see cref="FinishArgument"/>, the path to replace and its own process id, and ends; the new exe waits until the
/// old one has ended, copies itself over the old file (retrying while the virus scanner still looks at it), starts the
/// replaced file with the player's own arguments in the same console, and ends. The replaced build removes the
/// <c>.new</c> leftover at its next start (<see cref="RemoveLeftover"/>). If the copy fails the old file stays as it
/// was, so the app still starts.
/// </summary>
public static class UpdateHandoff
{
    /// <summary>The first argument of a downloaded exe started to finish an update.</summary>
    public const string FinishArgument = "--finish-update";

    private const string NewSuffix = ".new";
    private const string OldSuffix = ".old";
    private const int TargetArgument = 1;
    private const int ProcessArgument = 2;
    private const int FinishArguments = 3;
    private const int CopyTries = 20;

    /// <summary>NOT DATA: how long the old build gets to end before the copy is tried anyway.</summary>
    private static readonly TimeSpan OldExitWait = TimeSpan.FromSeconds(15);

    /// <summary>NOT DATA: the pause between tries while the file is still held (a virus scan).</summary>
    private static readonly TimeSpan CopyRetry = TimeSpan.FromMilliseconds(250);

    /// <summary>Where the download goes, beside the exe it replaces.</summary>
    public static string NewPath(string exePath)
    {
        return exePath + NewSuffix;
    }

    /// <summary>The old build's side: the downloaded exe starts to replace <paramref name="exePath"/>.</summary>
    public static void Start(string downloaded, string exePath, IReadOnlyList<string> args)
    {
        var start = new ProcessStartInfo(downloaded) { UseShellExecute = false };
        start.ArgumentList.Add(FinishArgument);
        start.ArgumentList.Add(exePath);
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);
    }

    /// <summary>
    /// The new build's side, when it was started to finish an update: true, and the update is done (or given up) and
    /// the caller ends; false for an ordinary start.
    /// </summary>
    public static bool TryFinish(string? self, IReadOnlyList<string> args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (self is null || args.Count < FinishArguments || args[0] != FinishArgument)
        {
            return false;
        }

        string target = args[TargetArgument];
        WaitForExit(args[ProcessArgument]);

        if (!TryCopy(self, target))
        {
            output.WriteLine("The update could not replace the app (the file is held); it starts as it was.");
        }

        var start = new ProcessStartInfo(target) { UseShellExecute = false };

        foreach (var arg in args.Skip(FinishArguments))
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);

        return true;
    }

    /// <summary>The download an update left beside the exe, and an older build's leftover; gone once nothing runs them.</summary>
    public static void RemoveLeftover(string exePath)
    {
        foreach (var leftover in new[] { exePath + NewSuffix, exePath + OldSuffix })
        {
            try
            {
                File.Delete(leftover);
            }
            catch (Exception held) when (held is IOException or UnauthorizedAccessException)
            {
                // Still running (it is ending): the next start removes it.
            }
        }
    }

    private static void WaitForExit(string processId)
    {
        if (!int.TryParse(processId, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
        {
            return;
        }

        try
        {
            using var old = Process.GetProcessById(id);
            old.WaitForExit(OldExitWait);
        }
        catch (ArgumentException)
        {
            // Already ended.
        }
        catch (Exception unwatchable) when (unwatchable is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Not ours to watch: the copy's own retries wait for the file instead.
        }
    }

    private static bool TryCopy(string self, string target)
    {
        for (int attempt = 0; attempt < CopyTries; attempt++)
        {
            try
            {
                File.Copy(self, target, overwrite: true);

                return true;
            }
            catch (Exception held) when (held is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(CopyRetry);
            }
        }

        return false;
    }
}
