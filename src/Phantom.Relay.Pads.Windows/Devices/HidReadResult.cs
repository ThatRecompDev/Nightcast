// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Devices;

/// <summary>What a read of a HID device came back with.</summary>
internal enum HidReadResult
{
    /// <summary>An input report arrived.</summary>
    Report,

    /// <summary>No report yet: the wait ran out or <see cref="IHidDevice.Wake"/> ended it early.</summary>
    Idle,

    /// <summary>The device is gone (unplugged, switched off, out of range) or failed: it is read no more.</summary>
    Gone,
}
