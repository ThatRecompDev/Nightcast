// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Events;

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// The capability bitmaps a device answers <c>EVIOCGBIT</c> with (one bit per code, the kernel's longs read as bytes
/// on a little-endian machine), and what makes a device a pad: the first gamepad button (<c>BTN_SOUTH</c>) and a left
/// stick (<c>ABS_X</c>). A keyboard, a mouse, a DualSense's motion or touchpad device has not both.
/// </summary>
internal static class EvdevCapabilities
{
    /// <summary>The bytes of a key bitmap.</summary>
    public const int KeyBytes = (EvdevCodes.KeyMax + 1) / EvdevCodes.BitsPerByte;

    /// <summary>The bytes of an absolute-axis bitmap.</summary>
    public const int AxisBytes = EvdevCodes.AxisCount / EvdevCodes.BitsPerByte;

    /// <summary>The bytes of a force-feedback bitmap.</summary>
    public const int ForceFeedbackBytes = EvdevCodes.ForceFeedbackCount / EvdevCodes.BitsPerByte;

    /// <summary>True when bit <paramref name="code"/> of <paramref name="bits"/> is set (false past its end).</summary>
    public static bool Has(ReadOnlySpan<byte> bits, int code)
    {
        var index = code / EvdevCodes.BitsPerByte;

        if (index >= bits.Length)
        {
            return false;
        }

        return (bits[index] & (1 << (code % EvdevCodes.BitsPerByte))) != 0;
    }

    /// <summary>True for a device with a gamepad's first button and a left stick.</summary>
    public static bool IsGamepad(ReadOnlySpan<byte> keys, ReadOnlySpan<byte> axes)
    {
        return Has(keys, EvdevCodes.ButtonSouth) && Has(axes, EvdevCodes.AxisX);
    }
}
