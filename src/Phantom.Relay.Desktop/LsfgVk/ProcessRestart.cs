// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// Starts Nightcast again in this same process (<c>execv</c>): the same file, the same arguments, the same process id,
/// so Steam (and gamescope) keep it as the game that runs, and the environment as it is now, lsfg-vk's variables
/// included. Called only once the window closed and the stream ended cleanly.
/// </summary>
[SupportedOSPlatform("linux")]
public static unsafe class ProcessRestart
{
    /// <summary>The entries of <c>argv</c> beside the arguments: the program's path first, the null that ends it last.</summary>
    private const int PathAndEnd = 2;

    /// <summary>Does not return when it worked; else why not.</summary>
    public static string Exec(string path, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var arguments = new nint[args.Count + PathAndEnd];
        arguments[0] = Marshal.StringToCoTaskMemUTF8(path);

        for (var index = 0; index < args.Count; index++)
        {
            arguments[index + 1] = Marshal.StringToCoTaskMemUTF8(args[index]);
        }

        Console.Out.Flush();
        Console.Error.Flush();
        int error;

        fixed (nint* first = arguments)
        {
            _ = LibcEnvironmentMethods.Execute(path, (byte**)first);
            error = Marshal.GetLastPInvokeError();
        }

        foreach (var argument in arguments)
        {
            Marshal.FreeCoTaskMem(argument);
        }

        return $"execv failed (errno {error})";
    }
}
