// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Relay.Xbox.Auth;

namespace Phantom.Relay.Desktop.Setup;

/// <summary>
/// Helps the sign-in by code on this machine: the code goes on the clipboard and the page opens in the default browser,
/// so the player only pastes it. Either may fail (no browser, no clipboard); the code stays on the console either way,
/// for a phone.
/// </summary>
public static class SignInCodeHelper
{
    private const string ClipboardTool = "clip.exe";

    public static void Help(DeviceSignIn signIn, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(output);

        if (OperatingSystem.IsWindows() && TryCopy(signIn.UserCode))
        {
            output.WriteLine("The code is on the clipboard; the page opens in your browser.");
        }

        TryOpen(signIn.Page);
    }

    private static bool TryCopy(string code)
    {
        try
        {
            using var clip = Process.Start(new ProcessStartInfo(ClipboardTool) { RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true });

            if (clip is null)
            {
                return false;
            }

            clip.StandardInput.Write(code);
            clip.StandardInput.Close();
            clip.WaitForExit();

            return clip.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void TryOpen(Uri page)
    {
        try
        {
            using var browser = Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No browser to open: the page's address is on the console.
        }
    }
}
