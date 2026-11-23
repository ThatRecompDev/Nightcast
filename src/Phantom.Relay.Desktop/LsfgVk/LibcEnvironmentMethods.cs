// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The C library's environment and <c>execv</c>, from glibc's <c>libc.so.6</c> (SteamOS is a glibc system). <c>getenv</c>
/// returns the environment's own string, which is read, never freed. Errors come back as
/// <see cref="Marshal.GetLastPInvokeError"/> (errno).
/// </summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class LibcEnvironmentMethods
{
    private const string Library = "libc.so.6";

    [LibraryImport(Library, EntryPoint = "getenv", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetEnvironment(string name);

    [LibraryImport(Library, EntryPoint = "setenv", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SetEnvironment(string name, string value, int overwrite);

    [LibraryImport(Library, EntryPoint = "unsetenv", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int UnsetEnvironment(string name);

    /// <summary>Replaces this process's image with <paramref name="path"/>'s, the same process id; returns only when it failed.</summary>
    [LibraryImport(Library, EntryPoint = "execv", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Execute(string path, byte** arguments);
}
