// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Linux.Interop;

/// <summary>
/// The evdev <c>ioctl</c> requests, built as <c>linux/input.h</c> builds them with <c>_IOC(direction, 'E', number,
/// size)</c>: the direction in bits 30-31 (1 write, 2 read), the argument's size in bits 16-29, the type <c>'E'</c> in
/// bits 8-15 and the number in bits 0-7. The sizes are x86-64's and arm64's.
/// </summary>
internal static class EvdevRequests
{
    private const uint Write = 1u << 30;
    private const uint Read = 2u << 30;
    private const int SizeShift = 16;
    private const uint Type = 'E' << 8;

    /// <summary><c>struct input_id</c>: bus, vendor, product, version, each a u16.</summary>
    public const int IdentitySize = 8;

    /// <summary><c>struct input_absinfo</c>: value, minimum, maximum, fuzz, flat, resolution, each an s32.</summary>
    public const int AxisInfoSize = 24;

    /// <summary><c>struct ff_effect</c> on a 64-bit kernel (its union holds a pointer, so it is 8-aligned).</summary>
    public const int EffectSize = 48;

    private const uint IdentityNumber = 0x02;
    private const uint KeysNumber = 0x18;
    private const uint BitsNumber = 0x20;
    private const uint AxisNumber = 0x40;
    private const uint UploadNumber = 0x80;
    private const uint RemoveNumber = 0x81;

    /// <summary><c>EVIOCGID</c>: the device's bus, vendor, product and version.</summary>
    public const uint Identity = Read | (IdentitySize << SizeShift) | Type | IdentityNumber;

    /// <summary><c>EVIOCSFF</c>: uploads (or, with its id, updates) a force-feedback effect.</summary>
    public const uint Upload = Write | (EffectSize << SizeShift) | Type | UploadNumber;

    /// <summary><c>EVIOCRMFF</c>: removes an effect by id (the argument is the id itself).</summary>
    public const uint Remove = Write | (sizeof(int) << SizeShift) | Type | RemoveNumber;

    /// <summary><c>EVIOCGBIT(type, length)</c>: the bitmap of the codes of one event type (0: the types themselves).</summary>
    public static uint Bits(int eventType, int length)
    {
        return Read | ((uint)length << SizeShift) | Type | (BitsNumber + (uint)eventType);
    }

    /// <summary><c>EVIOCGKEY(length)</c>: the bitmap of the keys held now.</summary>
    public static uint Keys(int length)
    {
        return Read | ((uint)length << SizeShift) | Type | KeysNumber;
    }

    /// <summary><c>EVIOCGABS(axis)</c>: one axis' value and range.</summary>
    public static uint Axis(int axis)
    {
        return Read | (AxisInfoSize << SizeShift) | Type | (AxisNumber + (uint)axis);
    }
}
