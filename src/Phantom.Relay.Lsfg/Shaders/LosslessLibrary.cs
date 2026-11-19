// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Phantom.Relay.Lsfg.Interop;

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>
/// The user's own Lossless.dll, mapped as a data file (LoadLibraryEx with LOAD_LIBRARY_AS_DATAFILE: none of its code
/// runs), and its RT_RCDATA resources by numeric id. A resource's bytes stay in the mapped file and are valid until
/// the library is disposed; nothing is copied out of it.
/// </summary>
public sealed unsafe class LosslessLibrary : IDisposable
{
    // EnumResNameProcW's return value that asks for the next resource.
    private const int ContinueEnumeration = 1;

    private readonly List<int> _ids;
    private nint _module;

    private LosslessLibrary(nint module, List<int> ids)
    {
        _module = module;
        _ids = ids;
    }

    /// <summary>The numeric ids of the RT_RCDATA resources, in the order the file lists them.</summary>
    public IReadOnlyList<int> ResourceIds => _ids;

    /// <summary>Maps the file and lists its raw-data resources; null and why when it cannot.</summary>
    public static LosslessLibrary? TryOpen(string path, out string? problem)
    {
        if (!File.Exists(path))
        {
            problem = "Lossless.dll was not found at the configured path";

            return null;
        }

        var module = LosslessNativeMethods.LoadLibraryEx(path, 0, LosslessNativeMethods.LoadAsDataFile | LosslessNativeMethods.LoadAsImageResource);

        if (module == 0)
        {
            problem = $"Lossless.dll could not be mapped as data (error {Marshal.GetLastPInvokeError()})";

            return null;
        }

        var ids = new List<int>();
        var handle = GCHandle.Alloc(ids);

        try
        {
            _ = LosslessNativeMethods.EnumResourceNames(module, LosslessNativeMethods.RawDataType, &Collect, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        problem = null;

        return new LosslessLibrary(module, ids);
    }

    /// <summary>A resource's bytes, or empty when the id is not there.</summary>
    public ReadOnlySpan<byte> Read(int id)
    {
        ObjectDisposedException.ThrowIf(_module == 0, this);
        var resource = LosslessNativeMethods.FindResource(_module, id, LosslessNativeMethods.RawDataType);

        if (resource == 0)
        {
            return [];
        }

        var size = LosslessNativeMethods.SizeofResource(_module, resource);
        var data = LosslessNativeMethods.LockResource(LosslessNativeMethods.LoadResource(_module, resource));

        if (data == 0)
        {
            return [];
        }

        return new ReadOnlySpan<byte>((void*)data, (int)size);
    }

    public void Dispose()
    {
        if (_module == 0)
        {
            return;
        }

        _ = LosslessNativeMethods.FreeLibrary(_module);
        _module = 0;
    }

    [UnmanagedCallersOnly]
    private static int Collect(nint module, nint type, nint name, nint parameter)
    {
        var ids = Unsafe.As<List<int>>(GCHandle.FromIntPtr(parameter).Target);

        if (ids is not null && (nuint)name < LosslessNativeMethods.IntegerResourceLimit)
        {
            ids.Add((int)name);
        }

        return ContinueEnumeration;
    }
}
