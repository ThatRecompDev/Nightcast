// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>kernel32's module loading, through source-generated imports: a library by its full path, its exports, and its release.</summary>
internal static partial class FrucNativeMethods
{
    /// <summary>
    /// LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 (libloaderapi.h): a library's own imports are
    /// looked for in its folder and in System32 only, never in the current directory or on PATH.
    /// </summary>
    public const uint SearchOwnFolderAndSystem = 0x100 | 0x800;

    private const string Kernel = "kernel32.dll";

    [LibraryImport(Kernel, EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadLibraryEx(string path, nint file, uint flags);

    [LibraryImport(Kernel, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint GetProcAddress(nint module, string name);

    [LibraryImport(Kernel)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FreeLibrary(nint module);
}
