// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary>Windows' HID library (<c>hid.dll</c>): the HID interface class, a device's ids, its report lengths, features.</summary>
internal static unsafe partial class HidMethods
{
    /// <summary><c>HIDP_STATUS_SUCCESS</c>.</summary>
    public const int Success = 0x00110000;

    private const string Library = "hid.dll";

    /// <summary><c>HidD_GetHidGuid</c>: the device interface class every HID collection registers under.</summary>
    [LibraryImport(Library, EntryPoint = "HidD_GetHidGuid")]
    public static partial void GetHidGuid(out Guid hidClass);

    /// <summary><c>HidD_GetAttributes</c>: the vendor and product id; works on a handle opened without access.</summary>
    [LibraryImport(Library, EntryPoint = "HidD_GetAttributes")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetAttributes(SafeFileHandle device, ref HidAttributes attributes);

    [LibraryImport(Library, EntryPoint = "HidD_GetPreparsedData")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPreparsedData(SafeFileHandle device, out nint preparsed);

    [LibraryImport(Library, EntryPoint = "HidD_FreePreparsedData")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreePreparsedData(nint preparsed);

    /// <summary><c>HidP_GetCaps</c>: the report lengths, from the preparsed data; <see cref="Success"/> when read.</summary>
    [LibraryImport(Library, EntryPoint = "HidP_GetCaps")]
    public static partial int GetCaps(nint preparsed, out HidCaps caps);

    /// <summary><c>HidD_GetFeature</c>: reads the feature report whose id is the buffer's first byte.</summary>
    [LibraryImport(Library, EntryPoint = "HidD_GetFeature", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetFeature(SafeFileHandle device, byte* report, uint length);
}
