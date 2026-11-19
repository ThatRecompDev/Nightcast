// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary>Windows' configuration manager (<c>cfgmgr32.dll</c>): the paths of the device interfaces of a class.</summary>
internal static unsafe partial class ConfigManagerMethods
{
    /// <summary><c>CR_SUCCESS</c>.</summary>
    public const uint Success = 0;

    /// <summary><c>CR_BUFFER_SMALL</c>: a device arrived between asking the size and asking the list.</summary>
    public const uint BufferSmall = 0x1A;

    /// <summary><c>CM_GET_DEVICE_INTERFACE_LIST_PRESENT</c>: only the interfaces present now.</summary>
    public const uint Present = 0;

    private const string Library = "cfgmgr32.dll";

    /// <summary><c>CM_Get_Device_Interface_List_SizeW</c>: the list's length in characters, its last terminator included.</summary>
    [LibraryImport(Library, EntryPoint = "CM_Get_Device_Interface_List_SizeW")]
    public static partial uint GetInterfaceListSize(out uint length, ref Guid interfaceClass, char* deviceId, uint flags);

    /// <summary><c>CM_Get_Device_Interface_ListW</c>: the paths, each ended by a zero, the list by one more.</summary>
    [LibraryImport(Library, EntryPoint = "CM_Get_Device_Interface_ListW")]
    public static partial uint GetInterfaceList(ref Guid interfaceClass, char* deviceId, char* buffer, uint length, uint flags);
}
