// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// NVIDIA's FRUC library loaded from the owner's SDK folder: the CUDA runtime beside it first, by its full path, then
/// NvOFFRUC.dll, each with its imports looked for in its own folder and System32 only (never the current directory or
/// PATH), and the five exports as function pointers. The modules are released on <see cref="Dispose"/>.
/// </summary>
internal sealed unsafe class FrucLibrary : IDisposable
{
    private nint _runtime;
    private nint _library;

    private FrucLibrary(nint runtime, nint library)
    {
        _runtime = runtime;
        _library = library;
    }

    public delegate* unmanaged<FrucCreateParameters*, nint*, int> Create { get; private set; }

    public delegate* unmanaged<nint, FrucRegistration*, int> Register { get; private set; }

    public delegate* unmanaged<nint, FrucUnregistration*, int> Unregister { get; private set; }

    public delegate* unmanaged<nint, FrucProcessInput*, FrucProcessOutput*, int> Process { get; private set; }

    public delegate* unmanaged<nint, int> Destroy { get; private set; }

    /// <summary>The library in <paramref name="folder"/>; null and why when a file is missing, will not load, or lacks an export.</summary>
    public static FrucLibrary? TryLoad(string folder, out string? problem)
    {
        var runtime = Load(Path.Combine(folder, FrucValues.CudaRuntimeFile), out problem);

        if (runtime == 0)
        {
            return null;
        }

        var library = Load(Path.Combine(folder, FrucValues.LibraryFile), out problem);

        if (library == 0)
        {
            _ = FrucNativeMethods.FreeLibrary(runtime);

            return null;
        }

        var loaded = new FrucLibrary(runtime, library);
        problem = loaded.Bind();

        if (problem is not null)
        {
            loaded.Dispose();

            return null;
        }

        return loaded;
    }

    public void Dispose()
    {
        if (_library != 0)
        {
            _ = FrucNativeMethods.FreeLibrary(_library);
            _library = 0;
        }

        if (_runtime != 0)
        {
            _ = FrucNativeMethods.FreeLibrary(_runtime);
            _runtime = 0;
        }
    }

    private static nint Load(string path, out string? problem)
    {
        problem = null;

        if (!File.Exists(path))
        {
            problem = $"{Path.GetFileName(path)} is not in the FRUC folder";

            return 0;
        }

        var module = FrucNativeMethods.LoadLibraryEx(path, 0, FrucNativeMethods.SearchOwnFolderAndSystem);

        if (module == 0)
        {
            problem = $"{Path.GetFileName(path)} would not load (error {Marshal.GetLastPInvokeError()})";
        }

        return module;
    }

    private string? Bind()
    {
        var create = FrucNativeMethods.GetProcAddress(_library, FrucValues.CreateName);
        var register = FrucNativeMethods.GetProcAddress(_library, FrucValues.RegisterName);
        var unregister = FrucNativeMethods.GetProcAddress(_library, FrucValues.UnregisterName);
        var process = FrucNativeMethods.GetProcAddress(_library, FrucValues.ProcessName);
        var destroy = FrucNativeMethods.GetProcAddress(_library, FrucValues.DestroyName);

        if (create == 0 || register == 0 || unregister == 0 || process == 0 || destroy == 0)
        {
            return $"{FrucValues.LibraryFile} lacks one of its five exports";
        }

        Create = (delegate* unmanaged<FrucCreateParameters*, nint*, int>)create;
        Register = (delegate* unmanaged<nint, FrucRegistration*, int>)register;
        Unregister = (delegate* unmanaged<nint, FrucUnregistration*, int>)unregister;
        Process = (delegate* unmanaged<nint, FrucProcessInput*, FrucProcessOutput*, int>)process;
        Destroy = (delegate* unmanaged<nint, int>)destroy;

        return null;
    }
}
