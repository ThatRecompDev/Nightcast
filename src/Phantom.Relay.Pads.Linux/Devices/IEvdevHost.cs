// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Devices;

/// <summary>
/// The system's input devices: the port between the pads' discovery and the platform (Linux's evdev, or a test's
/// fake), so discovery and hot-plug run without hardware. Called from the discovery thread only.
/// </summary>
internal interface IEvdevHost
{
    /// <summary>Adds the path of every event device present now (<c>/dev/input/event*</c>) to <paramref name="paths"/>.</summary>
    void Enumerate(List<string> paths);

    /// <summary>
    /// Asks the device at <paramref name="path"/> what it is: false when it cannot be asked (no permission, gone);
    /// <paramref name="gamepad"/> false for a device that is no pad (<see cref="EvdevCapabilities.IsGamepad"/>).
    /// </summary>
    bool TryDescribe(string path, out bool gamepad, out EvdevDescription? description);

    /// <summary>Opens the device for reading (and rumble where it can be written); null when it cannot be opened now.</summary>
    IEvdevDevice? Open(string path, EvdevDescription description);
}
