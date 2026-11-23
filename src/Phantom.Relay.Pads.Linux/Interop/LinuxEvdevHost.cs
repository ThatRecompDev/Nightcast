// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Runtime.Versioning;
using Phantom.Relay.Pads.Linux.Devices;
using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Interop;

/// <summary>
/// Linux's event devices: the files <c>/dev/input/event*</c>, each asked what it is with the evdev <c>ioctl</c>s
/// (<see cref="EvdevRequests"/>). A desktop session gives the seated user access to pads (systemd's <c>uaccess</c>);
/// a device that cannot be opened is skipped until the next look.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class LinuxEvdevHost : IEvdevHost
{
    private const string Folder = "/dev/input";
    private const string Pattern = "event*";

    /// <summary>The offsets of the vendor and the product in <c>struct input_id</c>.</summary>
    private const int VendorOffset = 2;
    private const int ProductOffset = 4;

    /// <summary>The offsets of the value, minimum and maximum in <c>struct input_absinfo</c>.</summary>
    private const int ValueOffset = 0;
    private const int MinimumOffset = 4;
    private const int MaximumOffset = 8;

    public void Enumerate(List<string> paths)
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }

        paths.AddRange(Directory.EnumerateFiles(Folder, Pattern));
    }

    public bool TryDescribe(string path, out bool gamepad, out EvdevDescription? description)
    {
        gamepad = false;
        description = null;
        var descriptor = LibcMethods.Open(path, LibcMethods.ReadOnly | LibcMethods.NonBlocking | LibcMethods.CloseOnExec);

        if (descriptor < 0)
        {
            return false;
        }

        try
        {
            description = Describe(descriptor, out gamepad);

            return description is not null || !gamepad;
        }
        finally
        {
            _ = LibcMethods.Close(descriptor);
        }
    }

    public IEvdevDevice? Open(string path, EvdevDescription description)
    {
        var flags = LibcMethods.NonBlocking | LibcMethods.CloseOnExec;
        var writable = true;
        var descriptor = LibcMethods.Open(path, LibcMethods.ReadWrite | flags);

        // A device the user may read but not write still plays, without rumble.
        if (descriptor < 0)
        {
            writable = false;
            descriptor = LibcMethods.Open(path, LibcMethods.ReadOnly | flags);
        }

        if (descriptor < 0)
        {
            return null;
        }

        var wake = LibcMethods.EventDescriptor(0, flags);

        if (wake < 0)
        {
            _ = LibcMethods.Close(descriptor);

            return null;
        }

        return new LinuxEvdevDevice(descriptor, wake, description, writable && description.Rumble);
    }

    /// <summary>The device's identity, axes and rumble; null for a device that is no pad or would not answer.</summary>
    private static EvdevDescription? Describe(int descriptor, out bool gamepad)
    {
        Span<byte> keys = stackalloc byte[EvdevCapabilities.KeyBytes];
        Span<byte> axes = stackalloc byte[EvdevCapabilities.AxisBytes];
        gamepad = Bits(descriptor, EvdevCodes.Key, keys) && Bits(descriptor, EvdevCodes.Absolute, axes) && EvdevCapabilities.IsGamepad(keys, axes);

        if (!gamepad)
        {
            return null;
        }

        Span<byte> identity = stackalloc byte[EvdevRequests.IdentitySize];

        fixed (byte* pointer = identity)
        {
            if (LibcMethods.Ioctl(descriptor, EvdevRequests.Identity, pointer) < 0)
            {
                return null;
            }
        }

        var vendor = BinaryPrimitives.ReadUInt16LittleEndian(identity[VendorOffset..]);
        var product = BinaryPrimitives.ReadUInt16LittleEndian(identity[ProductOffset..]);

        return new EvdevDescription(vendor, product, Ranges(descriptor, axes), HasRumble(descriptor));
    }

    private static EvdevAxisRange[] Ranges(int descriptor, ReadOnlySpan<byte> axes)
    {
        var ranges = new EvdevAxisRange[EvdevCodes.AxisCount];

        for (var code = 0; code < ranges.Length; code++)
        {
            if (EvdevCapabilities.Has(axes, code))
            {
                ranges[code] = Range(descriptor, code);
            }
        }

        return ranges;
    }

    /// <summary>One axis' value and range (<c>EVIOCGABS</c>); the default (absent) when it would not answer.</summary>
    internal static EvdevAxisRange Range(int descriptor, int code)
    {
        Span<byte> info = stackalloc byte[EvdevRequests.AxisInfoSize];

        fixed (byte* pointer = info)
        {
            if (LibcMethods.Ioctl(descriptor, EvdevRequests.Axis(code), pointer) < 0)
            {
                return default;
            }
        }

        var value = BinaryPrimitives.ReadInt32LittleEndian(info[ValueOffset..]);
        var minimum = BinaryPrimitives.ReadInt32LittleEndian(info[MinimumOffset..]);
        var maximum = BinaryPrimitives.ReadInt32LittleEndian(info[MaximumOffset..]);

        return new EvdevAxisRange(value, minimum, maximum);
    }

    private static bool HasRumble(int descriptor)
    {
        Span<byte> effects = stackalloc byte[EvdevCapabilities.ForceFeedbackBytes];

        return Bits(descriptor, EvdevCodes.ForceFeedback, effects) && EvdevCapabilities.Has(effects, EvdevCodes.Rumble);
    }

    /// <summary>The bitmap of the codes of <paramref name="eventType"/> (<c>EVIOCGBIT</c>); false when it would not answer.</summary>
    private static bool Bits(int descriptor, int eventType, Span<byte> bits)
    {
        bits.Clear();

        fixed (byte* pointer = bits)
        {
            return LibcMethods.Ioctl(descriptor, EvdevRequests.Bits(eventType, bits.Length), pointer) >= 0;
        }
    }
}
