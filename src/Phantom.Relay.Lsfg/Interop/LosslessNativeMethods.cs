// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>
/// The flat functions the loader needs, through source-generated imports: kernel32's resource reading (a module
/// mapped as data, never run) and D3DReflect from d3dcompiler_47, which ships with Windows. Nothing here compiles a
/// shader.
/// </summary>
internal static unsafe partial class LosslessNativeMethods
{
    /// <summary>LOAD_LIBRARY_AS_DATAFILE (libloaderapi.h): map the file for its resources; no code runs.</summary>
    public const uint LoadAsDataFile = 0x2;

    /// <summary>LOAD_LIBRARY_AS_IMAGE_RESOURCE (libloaderapi.h): map it as an image so resource ids resolve.</summary>
    public const uint LoadAsImageResource = 0x20;

    /// <summary>RT_RCDATA (winuser.h): the raw-data resource type the shaders are stored as.</summary>
    public const nint RawDataType = 10;

    /// <summary>IS_INTRESOURCE (winuser.h): a resource name below this is a number, not a string pointer.</summary>
    public const nuint IntegerResourceLimit = 0x10000;

    private const string Kernel = "kernel32.dll";
    private const string ShaderCompiler = "d3dcompiler_47.dll";

    [LibraryImport(Kernel, EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadLibraryEx(string path, nint file, uint flags);

    [LibraryImport(Kernel)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FreeLibrary(nint module);

    [LibraryImport(Kernel, EntryPoint = "EnumResourceNamesW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumResourceNames(nint module, nint type, delegate* unmanaged<nint, nint, nint, nint, int> callback, nint parameter);

    [LibraryImport(Kernel, EntryPoint = "FindResourceW")]
    internal static partial nint FindResource(nint module, nint name, nint type);

    [LibraryImport(Kernel)]
    internal static partial uint SizeofResource(nint module, nint resource);

    [LibraryImport(Kernel)]
    internal static partial nint LoadResource(nint module, nint resource);

    [LibraryImport(Kernel)]
    internal static partial nint LockResource(nint resource);

    [LibraryImport(ShaderCompiler)]
    internal static partial int D3DReflect(byte* data, nuint size, in Guid interfaceId, out nint reflector);
}
