// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Devices;

/// <summary>
/// The system's HID devices: the port between the pads' discovery and the platform (Windows' HID API, or a test's
/// fake), so discovery and hot-plug run without hardware. Called from the discovery thread only.
/// </summary>
internal interface IHidHost
{
    /// <summary>Adds the path of every HID interface present now to <paramref name="paths"/>.</summary>
    void Enumerate(List<string> paths);

    /// <summary>The USB vendor and product id of the device at <paramref name="path"/>; false when it cannot be asked.</summary>
    bool TryIdentify(string path, out ushort vendor, out ushort product);

    /// <summary>Opens the device for reading and writing; null when it cannot be opened now (another program holds it).</summary>
    IHidDevice? Open(string path);
}
