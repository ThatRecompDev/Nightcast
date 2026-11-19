// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Phantom.Relay.Pads.Windows.Devices;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary>
/// The system's HID devices through Windows' own API: the configuration manager lists the HID interfaces present,
/// a handle opened without access asks a device's ids (so a keyboard or a pad another program holds is asked without
/// disturbing it), and <see cref="WindowsHidDevice"/> opens a pad for overlapped reads and writes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WindowsHidHost : IHidHost
{
    /// <summary>A device can arrive between asking the list's size and the list: ask again, a few times at most.</summary>
    private const int ListAttempts = 3;

    public void Enumerate(List<string> paths)
    {
        HidMethods.GetHidGuid(out var hidClass);

        for (var attempt = 0; attempt < ListAttempts; attempt++)
        {
            if (ConfigManagerMethods.GetInterfaceListSize(out var length, ref hidClass, null, ConfigManagerMethods.Present) != ConfigManagerMethods.Success)
            {
                return;
            }

            var list = new char[length];
            uint result;

            fixed (char* buffer = list)
            {
                result = ConfigManagerMethods.GetInterfaceList(ref hidClass, null, buffer, length, ConfigManagerMethods.Present);
            }

            if (result == ConfigManagerMethods.Success)
            {
                Split(list, paths);

                return;
            }

            if (result != ConfigManagerMethods.BufferSmall)
            {
                return;
            }
        }
    }

    public bool TryIdentify(string path, out ushort vendor, out ushort product)
    {
        vendor = 0;
        product = 0;

        using var handle = Kernel32Methods.CreateFile(
            path, Kernel32Methods.NoAccess, Kernel32Methods.ShareRead | Kernel32Methods.ShareWrite, 0, Kernel32Methods.OpenExisting, Kernel32Methods.NoFlags, 0);

        if (handle.IsInvalid)
        {
            return false;
        }

        var attributes = new HidAttributes { Size = (uint)Unsafe.SizeOf<HidAttributes>() };

        if (!HidMethods.GetAttributes(handle, ref attributes))
        {
            return false;
        }

        vendor = attributes.VendorId;
        product = attributes.ProductId;

        return true;
    }

    public IHidDevice? Open(string path)
    {
        return WindowsHidDevice.TryOpen(path);
    }

    /// <summary>The paths of a list of zero-ended strings that ends with an empty one.</summary>
    private static void Split(ReadOnlySpan<char> list, List<string> paths)
    {
        while (list.Length > 0 && list[0] != '\0')
        {
            var end = list.IndexOf('\0');

            if (end < 0)
            {
                end = list.Length;
            }

            paths.Add(new string(list[..end]));
            list = list[Math.Min(end + 1, list.Length)..];
        }
    }
}
