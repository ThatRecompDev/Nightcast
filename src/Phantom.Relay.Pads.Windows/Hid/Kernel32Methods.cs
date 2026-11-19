// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary>The file calls of <c>kernel32.dll</c> a HID device is read and written with, overlapped.</summary>
internal static unsafe partial class Kernel32Methods
{
    public const uint GenericRead = 0x80000000;
    public const uint GenericWrite = 0x40000000;

    /// <summary>No access: enough to ask a device's attributes, even one another program holds.</summary>
    public const uint NoAccess = 0;

    public const uint ShareRead = 0x00000001;
    public const uint ShareWrite = 0x00000002;
    public const uint OpenExisting = 3;
    public const uint NoFlags = 0;
    public const uint FlagOverlapped = 0x40000000;

    /// <summary><c>ERROR_IO_PENDING</c>: an overlapped read or write was started and has not finished.</summary>
    public const int ErrorIoPending = 997;

    private const string Library = "kernel32.dll";

    [LibraryImport(Library, EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [LibraryImport(Library, EntryPoint = "ReadFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReadFile(SafeFileHandle file, byte* buffer, uint length, nint read, NativeOverlapped* overlapped);

    [LibraryImport(Library, EntryPoint = "WriteFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WriteFile(SafeFileHandle file, byte* buffer, uint length, nint written, NativeOverlapped* overlapped);

    [LibraryImport(Library, EntryPoint = "GetOverlappedResult", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetOverlappedResult(SafeFileHandle file, NativeOverlapped* overlapped, out uint transferred, [MarshalAs(UnmanagedType.Bool)] bool wait);

    [LibraryImport(Library, EntryPoint = "CancelIoEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CancelIoEx(SafeFileHandle file, NativeOverlapped* overlapped);
}
