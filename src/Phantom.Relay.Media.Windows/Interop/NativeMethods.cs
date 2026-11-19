// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>The flat functions of Media Foundation (mfplat), COM (ole32) and D3D11, through source-generated imports.</summary>
internal static partial class NativeMethods
{
    private const string MediaFoundation = "mfplat.dll";
    private const string Com = "ole32.dll";
    private const string Direct3D11 = "d3d11.dll";
    private const string Dxgi = "dxgi.dll";
    private const string Kernel = "kernel32.dll";

    [LibraryImport(Dxgi)]
    internal static partial int CreateDXGIFactory1(in Guid interfaceId, out nint factory);

    [LibraryImport(Kernel, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport(MediaFoundation)]
    internal static partial int MFStartup(uint version, uint flags);

    [LibraryImport(MediaFoundation)]
    internal static partial int MFShutdown();

    [LibraryImport(MediaFoundation)]
    internal static partial int MFCreateSample(out nint sample);

    [LibraryImport(MediaFoundation)]
    internal static partial int MFCreateMemoryBuffer(uint maxLength, out nint buffer);

    [LibraryImport(MediaFoundation)]
    internal static partial int MFCreateMediaType(out nint type);

    [LibraryImport(MediaFoundation)]
    internal static partial int MFCreateDXGIDeviceManager(out uint resetToken, out nint manager);

    [LibraryImport(Com)]
    internal static partial int CoInitializeEx(nint reserved, uint apartment);

    [LibraryImport(Com)]
    internal static partial int CoCreateInstance(in Guid classId, nint outer, uint context, in Guid interfaceId, out nint instance);

    [LibraryImport(Direct3D11)]
    internal static partial int D3D11CreateDevice(
        nint adapter,
        int driverType,
        nint software,
        uint flags,
        nint featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out nint device,
        out int featureLevel,
        out nint immediateContext);
}
